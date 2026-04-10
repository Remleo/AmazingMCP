# ProjectDesign — высокоуровневая карта дизайна решения

## Назначение

`ProjectDesignService` строит высокоуровневую карту дизайна C#-решения: группы абстракций и зависимости между ними. В отличие от `DependencyMap`, который оперирует отдельными типами, ProjectDesign работает на уровне групп — показывая архитектурную структуру решения без деталей конкретных классов и интерфейсов.

MCP tool: `get_project_design`

## Что показывает

Плоский список групп абстракций (без разделения по проектам). По каждой группе:

- Короткое имя (относительно root namespace проекта)
- Полное имя (полный namespace)
- Количество записей (entries) в группе
- Зависимости на другие группы (полные namespace целевых групп)

## Что попадает в группы

Группы формируются только из абстракций с `SourceFilePath != null` — то есть типов, определённых в исходном коде решения. NuGet-типы (с `SourceFilePath = null`) в группы не попадают, но участвуют в резолве зависимостей: если имплементация зависит от NuGet-типа, его namespace появится в `DependsOn` соответствующей группы.

Типы из тест-проектов исключаются на уровне `DependencyMapService` и в группы не попадают.

## Группировка

Абстракции группируются по namespace. Root namespace определяется из `<RootNamespace>` в `.csproj`, при отсутствии — используется имя проекта. Короткое имя группы вычисляется относительно root namespace.

| Namespace | Root namespace | Короткое имя |
|---|---|---|
| `MyApp.Core` | `MyApp.Core` | `(root)` |
| `MyApp.Core.Services` | `MyApp.Core` | `Services` |
| `MyApp.Core.Mapping.Tv2` | `MyApp.Core` | `Mapping.Tv2` |

## Зависимости между группами

Для каждой группы собираются внешние зависимости — те, что выходят за рамки группы. Алгоритм:

1. Берём все абстракции группы
2. Для каждой абстракции берём все имплементации
3. Для каждой имплементации проходим по цепочке базовых классов
4. Собираем все constructor-injected зависимости
5. Фильтруем: оставляем только те, что являются известными абстракциями и не входят в текущую группу
6. Резолвим каждую зависимость в полный namespace целевой группы

NuGet-зависимости резолвятся в namespace внешней библиотеки (например `AutoMapper`, `Microsoft.Extensions.Logging`) и попадают в `DependsOn` наравне с source-группами.

## Пример вывода

```markdown
# Project Design

## Configuration (TestProject.Core.Configuration)
Entries count: 1

## EventHandling (TestProject.Core.EventHandling)
Entries count: 3

## Logging (TestProject.Core.Logging)
Entries count: 1

## Mapping (TestProject.App.Mapping)
Entries count: 5
Depends on:
- → AutoMapper

## Messaging (TestProject.App.Messaging)
Entries count: 3
Depends on:
- → TestProject.App.Mapping
- → TestProject.Core.EventHandling

## Notifications (TestProject.Core.Notifications)
Entries count: 1

## Persistence (TestProject.Core.Persistence)
Entries count: 3

## Services (TestProject.App.Services)
Entries count: 3
Depends on:
- → TestProject.Core.Configuration
- → TestProject.Core.Persistence
- → TestProject.Core.Services

## Services (TestProject.Core.Services)
Entries count: 3
Depends on:
- → TestProject.Core.Persistence
```

## Что НЕ попадает в вывод

- Имена конкретных абстракций (интерфейсов, классов)
- Имена имплементаций
- Детали constructor injection
- Member usages
- Namespace-группы без собственных source-defined абстракций
- Типы из тест-проектов
- NuGet-типы как самостоятельные группы (только как цели в `DependsOn`)

## Архитектура

`ProjectDesignService` использует `DependencyMapService` как источник данных:

```
ProjectDesignService
├── DependencyMapService.BuildMapAsync() → DependencyMapResult
├── Phase 1: группировка source-абстракций по namespace
│   └── пропускаем абстракции с SourceFilePath = null (NuGet)
├── Phase 2: lookup всех абстракций (включая NuGet) для резолва зависимостей
├── Phase 3: для каждой группы — CollectExternalDependencies()
│   └── walker по impl → base classes → constructor deps → resolve to namespace
├── ResolveRootNamespaces() → читает <RootNamespace> из .csproj файлов
├── ResolveOwningProject() → longest-prefix match namespace → project (для short name)
└── GetRelativeNamespace() → вычисляет короткое имя группы
```

## Модели

```
ProjectDesignResult
└── Groups: List<AbstractionGroup>
    ├── FullName (полный namespace)
    ├── Name (короткое имя, "" для root)
    ├── EntryCount
    └── DependsOn: List<string> (полные namespace целевых групп, включая NuGet)
```

## Тесты

Тесты расположены в `Tests/AmazingMCP.Tests/ProjectDesignServiceTests.cs`:

| Область | Покрытие |
|---|---|
| Flat groups | Source-группы присутствуют, Infrastructure отсутствует, sub-namespace (Mapping.Tv2/Tv3/Tv4) |
| NuGet exclusion | NuGet-типы не создают группы, но попадают в DependsOn |
| EntryCount | Количество записей в группах Services, Persistence |
| DependsOn | Cross-group deps, NuGet deps (AutoMapper), internal deps excluded |
| ResolveOwningProject | Exact match, longest-prefix, fallback |
| GetRelativeNamespace | Root, child, empty root, different root |
| ExtractRootNamespace | С тегом, без тега |
| Markdown | No project headers, group headers, Full name, Entries count label, Depends on with full namespaces |
