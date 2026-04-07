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

## Пример вывода

```markdown
# Project Design

## Contracts (TestProject.App.Contracts)
Entries count: 2

## Helpers (TestProject.App.Helpers)
Entries count: 1

## Services (TestProject.App.Services)
Entries count: 1

## Services.GenericConsumers (TestProject.App.Services.GenericConsumers)
Entries count: 1

## EventHandling (TestProject.Core.EventHandling)
Entries count: 1

## Events (TestProject.Core.Events)
Entries count: 2

## Logging (TestProject.Core.Logging)
Entries count: 1

## Mapping (TestProject.Core.Mapping)
Entries count: 2

## Mapping.Tv2 (TestProject.Core.Mapping.Tv2)
Entries count: 2

## Messaging (TestProject.Core.Messaging)
Entries count: 5
Depends on:
- → TestProject.Core.EventHandling
- → TestProject.Core.Logging
- → TestProject.Core.Notifications
- → TestProject.Core.Persistence

## Models (TestProject.Core.Models)
Entries count: 2

## Notifications (TestProject.Core.Notifications)
Entries count: 1

## Persistence (TestProject.Core.Persistence)
Entries count: 3

## Services (TestProject.Core.Services)
Entries count: 3
Depends on:
- → TestProject.Core.Models
- → TestProject.Core.Persistence
```

## Что НЕ попадает в вывод

- Имена конкретных абстракций (интерфейсов, классов)
- Имена имплементаций
- Детали constructor injection
- Member usages
- Namespace-группы без собственных абстракций

## Архитектура

`ProjectDesignService` использует `DependencyMapService` как источник данных и строит поверх него группировку:

```
ProjectDesignService
├── DependencyMapService.BuildMapAsync() → DependencyMapResult
├── ResolveRootNamespaces() → читает <RootNamespace> из .csproj файлов
├── ResolveOwningProject() → longest-prefix match namespace → project (для short name)
├── GetRelativeNamespace() → вычисляет короткое имя группы
└── CollectExternalDependencies() → резолвит зависимости в полные namespace целевых групп
```

## Модели

```
ProjectDesignResult
└── Groups: List<AbstractionGroup>
    ├── FullName (полный namespace)
    ├── Name (короткое имя, "" для root)
    ├── EntryCount
    └── DependsOn: List<string> (полные namespace целевых групп)
```

## Тесты

Тесты расположены в `Tests/AmazingMCP.Tests/ProjectDesignServiceTests.cs`:

| Область | Покрытие |
|---|---|
| Flat groups | Core/App группы присутствуют, Infrastructure отсутствует, sub-namespace (Mapping.Tv2/Tv3/Tv4) |
| EntryCount | Количество записей в группах Services, Persistence |
| DependsOn | Cross-group deps (Messaging → Persistence, Logging, Notifications), internal deps excluded, валидация ссылок |
| ResolveOwningProject | Exact match, longest-prefix, fallback |
| GetRelativeNamespace | Root, child, empty root, different root |
| ExtractRootNamespace | С тегом, без тега |
| Markdown | No project headers, group headers, Full name, Entries count label, Depends on with full namespaces, отсутствие имён абстракций |
