# DependencyMap — карта зависимостей решения

## Назначение

`DependencyMapService` строит полную карту зависимостей C#-решения через Roslyn-анализ. Результат — два словаря: абстракции и их имплементации с деталями о constructor injection и использовании членов зависимостей.

MCP tool: `get_dependency_map`

## Что попадает в карту

### Абстракции (`Abstractions`)

Абстракция — это тип, который может быть зарегистрирован в DI-контейнере и инжектирован в другие классы.

| Категория | Условие попадания |
|---|---|
| Интерфейс | Есть хотя бы один source-defined конкретный класс-имплементор |
| Closed generic интерфейс | `IRepository<Animal>` — отдельная запись для каждой закрытой комбинации |
| Open generic интерфейс | `IRepository<T>` — если есть open generic имплементация |
| Abstract класс | Всегда, если определён в исходном коде решения |
| Конкретный класс без интерфейсов | Только если: не реализует ни один интерфейс (даже через цепочку базовых классов) И имеет конструктор хотя бы с одной сложной зависимостью |
| IOptions-тип | Класс `T` из `IOptions<T>` в конструкторе — добавляется через Phase 4 |
| Внешний (NuGet) тип | Добавляется с `SourceFilePath = null` если на него есть зависимость |

Исключаются:
- Системные интерфейсы (`System.*`, `Microsoft.Extensions.Options.*`, `IDisposable`, `IEnumerable<T>` и т.д.)
- Типы из тест-проектов (проекты с `Microsoft.NET.Test.Sdk` в `.csproj`)
- Partial-классы дедуплицируются по полному имени

### Имплементации (`Implementations`)

Конкретный (не abstract, не static) класс попадает в `Implementations` если:
- является имплементором хотя бы одной известной абстракции, ИЛИ
- сам является standalone-абстракцией (класс без интерфейсов с зависимостями)

Для каждой имплементации собирается:
- Реализуемые абстракции (включая из базовых классов)
- Цепочка базовых классов
- Constructor-injected зависимости
- Использование членов зависимостей (method calls, property get/set)

## Алгоритм построения

Построение идёт в 4 фазы:

**Phase 1 — сбор типов**
Собираются все source-defined типы из компиляций, исключая тест-проекты. Partial-классы дедуплицируются.

**Phase 2 — начальный набор абстракций**
- Интерфейсы с source-defined имплементорами
- Closed/open generic интерфейсы
- Abstract классы
- Конкретные классы без интерфейсов и с конструктором с зависимостями

**Phase 3 — анализ имплементаций**
Для каждого конкретного класса, являющегося имплементором или standalone-абстракцией: анализируются конструктор, базовые классы, member usages. Зависимости, которых нет в известных абстракциях, попадают в `missingAbstractions`.

**Phase 4 — итеративное дополнение**
Цикл по `missingAbstractions`:
- Если тип есть в исходном коде → добавляется как абстракция с полным анализом зависимостей, новые missing deps уходят в следующую итерацию
- Если тип из NuGet/внешней сборки → добавляется с `SourceFilePath = null`, без рекурсии

## Анализ конструкторов

Выбирается public конструктор с наибольшим числом параметров. Для каждого параметра:

- `IOptions<T>` / `IOptionsSnapshot<T>` / `IOptionsMonitor<T>` → разворачивается в `T` с флагом `IsOptions = true`
- `IEnumerable<T>` → разворачивается в `T` с флагом `IsEnumerable = true`
- Примитивы, enum, struct → пропускаются
- Остальное → записывается как зависимость

## Анализ использования членов

Для каждой имплементации анализируется тело класса и всех базовых классов. Обнаруживаются:

- Вызовы методов на зависимостях (`MemberUsageKind.MethodCall`)
- Чтение свойств (`MemberUsageKind.PropertyGet`)
- Запись свойств (`MemberUsageKind.PropertySet`)

## Фильтрация тест-проектов

`TestProjectFilter.ExcludeTestProjects()` исключает компиляции проектов, в `.csproj` которых есть `Microsoft.NET.Test.Sdk`. Применяется в `DependencyMapService` перед анализом типов. `WorkspaceProvider` загружает все проекты без фильтрации — фильтрация происходит только там, где нужна.

## Кеширование

Результат `BuildMapAsync` кешируется в `IMemoryCache` по полному пути к solution-файлу. Sliding expiration — 2 часа.

## Архитектура сервисов

`DependencyMapService` — оркестратор, делегирующий работу через DI:

| Сервис | Ответственность |
|---|---|
| `ITypeCollector` | Сбор source-defined типов, фильтрация системных интерфейсов, цепочка базовых классов, дедупликация partial-классов |
| `IConstructorAnalyzer` | Анализ конструкторов, unwrap IOptions/IEnumerable |
| `IMemberUsageAnalyzer` | Поиск вызовов методов и обращений к свойствам на зависимостях |
| `IAbstractionExtractor` | Построение `AbstractionInfo`, резолв closed generic интерфейсов, сбор declared members |
| `TestProjectFilter` | Статический хелпер — исключает тест-проекты из списка компиляций |

## Модели

```
DependencyMapResult
├── Abstractions: Dictionary<string, AbstractionInfo>
│   ├── FullName, Namespace, ProjectName
│   ├── SourceFilePath  (null для NuGet-типов)
│   ├── IsInterface
│   ├── DeclaredMembers: List<string>
│   └── Implementations: List<string>
└── Implementations: Dictionary<string, ImplementationInfo>
    ├── FullName, Namespace, ProjectName, SourceFilePath
    ├── ImplementedAbstractions: List<string>
    ├── BaseClasses: List<string>
    ├── Dependencies: List<ConstructorDependency>
    │   ├── TypeFullName
    │   ├── IsOptions
    │   └── IsEnumerable
    └── DependencyMemberUsages: List<MemberUsage>
        ├── MemberName
        └── Kind: MethodCall | PropertyGet | PropertySet
```

## Тесты

Тесты расположены в `Tests/AmazingMCP.Tests/` как partial-класс `DependencyMapServiceTests`:

| Файл | Покрытие |
|---|---|
| `DependencyMapServiceTests.cs` | Fixture setup, `Act()` |
| `DependencyMapServiceTests.Abstractions.cs` | Интерфейсы, IOptions, excluded system, abstract classes, NuGet-зависимости, фильтрация тест-проектов |
| `DependencyMapServiceTests.Implementations.cs` | Базовые имплементации, base class chain, multi-interface |
| `DependencyMapServiceTests.ConstructorDeps.cs` | Interface deps, IOptions unwrap, IEnumerable unwrap |
| `DependencyMapServiceTests.MemberUsages.cs` | Method call, property get, base class inheritance |
| `DependencyMapServiceTests.Generics.cs` | Closed/open generics, constructor deps на generics, member usages |
| `DependencyMapServiceTests.IEnumerableNonGeneric.cs` | IEnumerable<IMessageHandler>, IEnumerable<IAsyncEventHandler> |
