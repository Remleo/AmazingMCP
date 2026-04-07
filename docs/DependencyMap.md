# DependencyMap — карта зависимостей решения

## Назначение

`DependencyMapService` строит полную карту зависимостей C#-решения через Roslyn-анализ. Результат — два словаря: абстракции (интерфейсы, standalone-классы, IOptions-типы) и их имплементации с деталями о constructor injection и использовании членов зависимостей.

## Что попадает в карту

### Абстракции (`Abstractions`)

| Категория | Пример | Как попадает |
|---|---|---|
| Интерфейс с имплементацией | `IAnimalService` | Автоматически, если есть хотя бы один конкретный класс |
| Closed generic интерфейс | `IRepository<Animal>` | Отдельная запись для каждой закрытой комбинации типов |
| Open generic интерфейс | `IRepository<T>` | Если есть open generic имплементация (`GenericRepository<T>`) |
| Standalone класс | `StandaloneHelper` | Класс без интерфейсов — одновременно абстракция и имплементация |
| IOptions-тип | `AnimalSettings` | Автоматически при обнаружении `IOptions<T>` в конструкторе |

Системные интерфейсы (`IDisposable`, `IEnumerable<T>`, `IComparable` и т.д.) исключаются.

### Имплементации (`Implementations`)

Для каждого конкретного (не abstract, не static) класса собирается:

- Реализуемые абстракции (включая из базовых классов)
- Цепочка базовых классов
- Constructor-injected зависимости
- Использование членов зависимостей (method calls, property get/set)

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

## Кеширование

Результат `BuildMapAsync` кешируется в `IMemoryCache` по полному пути к solution-файлу. Sliding expiration — 2 часа.

## Архитектура сервисов

`DependencyMapService` — оркестратор, делегирующий работу через DI:

| Сервис | Ответственность |
|---|---|
| `ITypeCollector` | Сбор source-defined типов из компиляций, фильтрация системных интерфейсов, цепочка базовых классов |
| `IConstructorAnalyzer` | Анализ конструкторов, unwrap IOptions/IEnumerable |
| `IMemberUsageAnalyzer` | Поиск вызовов методов и обращений к свойствам на зависимостях |
| `IAbstractionExtractor` | Построение `AbstractionInfo`, резолв closed generic интерфейсов, сбор declared members |

## Модели

```
DependencyMapResult
├── Abstractions: Dictionary<string, AbstractionInfo>
│   ├── FullName, Namespace, ProjectName, SourceFilePath
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
| `DependencyMapServiceTests.Abstractions.cs` | Интерфейсы, standalone, IOptions, excluded system, abstract classes |
| `DependencyMapServiceTests.Implementations.cs` | Базовые имплементации, base class chain, multi-interface |
| `DependencyMapServiceTests.ConstructorDeps.cs` | Interface deps, IOptions unwrap, IEnumerable unwrap |
| `DependencyMapServiceTests.MemberUsages.cs` | Method call, property get, base class inheritance |
| `DependencyMapServiceTests.Generics.cs` | Closed/open generics, constructor deps на generics, member usages |
