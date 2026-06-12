#Requires -Version 5.1
<#
.SYNOPSIS
    Inyecta la politica de idioma del proyecto como additionalContext en cada SessionStart.

.DESCRIPTION
    Este script es invocado por el hook SessionStart definido en
    .github/hooks/spanish-docs-policy.json.

    Lee el payload JSON de VS Code desde stdin y emite un objeto JSON
    a stdout con la politica de idioma en espanol para que el LLM
    la tenga presente durante toda la sesion.

    La politica indica:
      - Documentos de requisitos (spec.md, plan.md, tasks.md, checklists,
        reportes, stakeholder docs, README, UI): SI en espanol.
      - Scripts de Spec Kit (.ps1, .sh), identificadores de codigo y
        comandos CLI: NO traducir.
#>
[CmdletBinding()]
param()

# 1. Leer payload del evento (informativo; este script no lo usa para variar la salida)
$stdin = ''
if (-not [Console]::IsInputRedirected) {
    # No input redirected (e.g., invoked from terminal directly). Proceed with defaults.
} else {
    $stdin = [Console]::In.ReadToEnd()
}
# Mantener por si en el futuro se necesita inspeccionar la sesion:
# $event = $stdin | ConvertFrom-Json -ErrorAction SilentlyContinue

# 2. Construir el texto de politica a inyectar
#    Formato: markdown compacto con secciones claras (positivo + negativo + ejemplos)
$policy = @"
POLITICA DE IDIOMA DEL PROYECTO (ContosoDashboard)
==================================================

REGLA POSITIVA - ESCRIBIR EN ESPANOL:
--------------------------------------
Todos los documentos GENERADOS por el agente como parte del trabajo de
requisitos DEBEN estar en espanol. Esto incluye, sin limitarse a:
  - specs/[NNN-feature]/spec.md, plan.md, tasks.md, research.md,
    data-model.md, quickstart.md, contracts/*.md
  - .specify/memory/constitution.md (enmiendas, secciones, comentarios)
  - StakeholderDocs/**/*.md (HU, HUT, HAB, documentacion de negocio)
  - Reportes generados por herramientas: code-quality-report.md,
    quality-reports/**.md, analisis, auditorias
  - README.md, docs/**/*.md, CHANGELOG.md
  - Mensajes visibles en la UI (textos en .razor que vea el usuario)
  - Comentarios de codigo fuente SOLO cuando sean visibles para el
    usuario final o expliquen logica de negocio; los comentarios
    tecnicos internos pueden quedar en espanol o ingles segun
    consistencia con el archivo.
  - Mensajes de commit, descripciones de PR, titulos de issues
    (cuando el usuario lo solicite en espanol).
  - Conversacion y respuestas del agente hacia el usuario.

REGLA NEGATIVA - NO TRADUCIR:
-----------------------------
Los siguientes elementos son tecnicos por naturaleza y DEBEN permanecer
en su idioma original (ingles). No traducir nombres, no "espanolizar"
comandos ni reescribir scripts:
  - Scripts de Spec Kit: .specify/extensions/**/scripts/*.ps1, *.sh
  - Scripts de los hooks: .github/hooks/scripts/*.ps1
  - Scripts del agente /code-quality: .github/scripts/*.ps1
  - Identificadores de codigo: nombres de clases, metodos, propiedades,
    variables, namespaces, assemblies, namespaces XAML, archivos .razor
    de componentes Blazor (Pages, Shared).
  - Comandos CLI y nombres de herramientas: dotnet, npm, npx, jscpd,
    reportgenerator, git, etc.
  - Comandos de PowerShell: Get-Item, Set-Location, Out-File, etc.
  - Terminos tecnicos universales: "Repository", "Service", "Controller",
    "Builder", "Factory", "DTO", "CRUD", "API", "Endpoint", "Middleware",
    "Pipeline", "Plugin", etc. Pueden ir acompanados de su traduccion
    en parentesis en el texto, pero el termino se conserva.
  - Nombres de paquetes NuGet y namespaces: Microsoft.AspNetCore.*,
    xUnit, Stryker.NET, bUnit, PactNet, etc.
  - Claves de configuracion y nombres de variables de entorno.
  - Llaves de diccionarios JSON / propiedades de objetos C#.
  - Mensajes de log estructurado (LogInformation("User {UserId} logged in")).

CRITERIO CUANDO HAYA DUDA:
--------------------------
Preguntate: "esto es generado para consumo humano o es un artefacto
maquina-ejecutable?". Si la respuesta es "humano" -> espanol.
Si la respuesta es "maquina" (compilador, runtime, parser, CLI) ->
ingles, aunque el contenido sea entendible por humanos.
"@

# 3. Convertir a JSON seguro (escapar correctamente) y emitir a stdout
$output = [ordered]@{
    continue                  = $true
    hookSpecificOutput        = [ordered]@{
        hookEventName        = 'SessionStart'
        additionalContext    = $policy
    }
}

# Forzar UTF-8 sin BOM para stdout
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$json = $output | ConvertTo-Json -Depth 5 -Compress
[Console]::Out.WriteLine($json)

exit 0
