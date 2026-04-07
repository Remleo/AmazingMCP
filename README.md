# AmazingMCP — Project Overview

## Что это

MCP-сервер (Model Context Protocol) на базе ASP.NET Core (.NET 10), который предоставляет AI-агентам возможность анализировать C#-решения через Roslyn.

Сервер открывает `.sln`/`.slnx` файлы, компилирует проекты в памяти и позволяет искать типы и получать подробную информацию о них — включая NuGet-зависимости.

## Документация

- [DependencyMap — карта зависимостей решения](docs/DependencyMap.md)
- [ProjectDesign — высокоуровневая карта дизайна решения](docs/ProjectDesign.md)

## Стек

- .NET 10, ASP.NET Core (Minimal API)
- Microsoft.CodeAnalysis (Roslyn) + MSBuild Workspaces
- ModelContextProtocol.AspNetCore 1.2.0
- HTTP-транспорт (порт 5275 в dev)