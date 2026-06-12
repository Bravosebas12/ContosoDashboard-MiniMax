# Hooks de GitHub Copilot

Este directorio contiene **hooks** (automatización determinista del ciclo de vida) para los agentes de GitHub Copilot en este workspace.

## 📋 Hooks disponibles

### `spanish-docs-policy.json` — Política de idioma

Inyecta automáticamente al inicio de cada sesión de Copilot la **política de idioma del proyecto**, recordándole al LLM que:

- ✅ **Todos los documentos generados** para el trabajo de requisitos (specs, plans, tasks, stakeholder docs, reportes, README, UI) **DEBEN estar en español**.
- ❌ **NO traducir** scripts de Spec Kit, identificadores de código, comandos CLI ni términos técnicos universales.

#### Disparador

- **Evento**: `SessionStart` (se ejecuta una vez al inicio de cada sesión nueva del agente).
- **Comando**: `powershell -File .github/hooks/scripts/inject-language-policy.ps1`
- **Timeout**: 10 segundos.

#### Salida

El script emite un objeto JSON a stdout con la política como `hookSpecificOutput.additionalContext`, que VS Code inyecta silenciosamente en el contexto del LLM. **No muestra nada al usuario.**

```json
{
  "continue": true,
  "hookSpecificOutput": {
    "hookEventName": "SessionStart",
    "additionalContext": "POLITICA DE IDIOMA DEL PROYECTO ..."
  }
}
```

#### Política resumida

| Tipo de artefacto | Idioma |
|-------------------|--------|
| `specs/**/*.md`, `plan.md`, `tasks.md`, `checklists` | 🇪🇸 Español |
| `StakeholderDocs/**` | 🇪🇸 Español |
| Reportes (`code-quality-report.md`, análisis) | 🇪🇸 Español |
| `README.md`, `docs/**` | 🇪🇸 Español |
| UI Blazor (textos visibles al usuario) | 🇪🇸 Español |
| Mensajes de commit / PR (cuando se solicite) | 🇪🇸 Español |
| Scripts Spec Kit (`.ps1`, `.sh`) | 🇬🇧 Inglés (no tocar) |
| Identificadores de código (clases, métodos) | 🇬🇧 Inglés (no tocar) |
| Comandos CLI y nombres de tools | 🇬🇧 Inglés (no tocar) |

#### Cómo probarlo

1. Iniciar una nueva sesión de Copilot en VS Code.
2. Pedirle al agente que cree o modifique un documento de requisitos (ej. "crea un spec para feature de notificaciones").
3. Verificar que el documento resultante esté en español.
4. Pedirle que modifique un script de Spec Kit y verificar que **NO** traduce ni los comentarios ni los identificadores.

#### Cómo extenderlo

Si necesitas agregar más exclusiones o aclaraciones (por ejemplo, traducir mensajes de log al español), edita la variable `$policy` en [scripts/inject-language-policy.ps1](scripts/inject-language-policy.ps1).

## 🔧 Estructura

````
.github/hooks/
├── README.md                                    Este archivo
├── spanish-docs-policy.json                     Definición del hook
└── scripts/
    └── inject-language-policy.ps1               Script PowerShell que emite la política
````

## 📚 Referencias

- VS Code Hooks: <https://code.visualstudio.com/docs/copilot/customization/hooks>
- Skill `agent-customization` / `hooks.md`: <c:\Users\bravo\AppData\Local\Programs\Microsoft VS Code\6928394f91\resources\app\extensions\copilot\assets\prompts\skills\agent-customization\references\hooks.md>
