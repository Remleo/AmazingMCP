# AmazingMCP — Project Overview

## Что это

MCP-сервер (Model Context Protocol) на базе ASP.NET Core (.NET 10), который предоставляет AI-агентам возможность анализировать C#-решения через Roslyn.

Сервер открывает `.sln`/`.slnx` файлы, компилирует проекты в памяти и позволяет искать типы, строить карту зависимостей и получать высокоуровневую карту архитектуры решения.

## Документация

- [DependencyMap — карта зависимостей решения](docs/DependencyMap.md)
- [ProjectDesign — высокоуровневая карта дизайна решения](docs/ProjectDesign.md)

## MCP Tools

| Tool | Описание |
|---|---|
| `get_project_design` | Высокоуровневая карта: группы абстракций по namespace и зависимости между ними |
| `get_detailed_project_design` | Детальный вид абстракций и имплементаций для указанных namespace-групп |
| `get_type_deps_and_usage` | Полная информация о зависимостях и использовании типа по запросу (exact, wildcard, fuzzy) |
| `query_symbol` | Поиск типов по имени (включая NuGet) |
| `get_symbol_info` | Детальная информация о типе (включая NuGet) |

## Стек

- .NET 10, ASP.NET Core (Minimal API)
- Microsoft.CodeAnalysis (Roslyn) + MSBuild Workspaces
- ModelContextProtocol.AspNetCore 1.2.0
- HTTP-транспорт (порт 5275 в dev)

## Ключевые особенности

- Тест-проекты (с `Microsoft.NET.Test.Sdk`) автоматически исключаются из анализа зависимостей
- NuGet-типы отслеживаются как зависимости (`SourceFilePath = null`), но не создают группы в ProjectDesign
- Partial-классы дедуплицируются
- Результаты кешируются с инкрементальной перекомпиляцией при изменении файлов
