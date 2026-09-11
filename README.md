# ApiChatbot

> API REST para chatbot de portafolio profesional impulsada por IA local (Ollama) con autenticación JWT anónima, analytics en Redis y contexto dinámico desde GitHub.

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Docker](https://img.shields.io/badge/Docker-24.0-2496ED?logo=docker)](https://www.docker.com/)
[![License](https://img.shields.io/badge/License-CC%20BY%204.0-lightgrey.svg)](LICENSE)

---

## Características

- **IA Local** — Respuestas generadas por Ollama (qwen2.5) sin depender de servicios externos de IA
- **Contexto Dinámico** — Construye automáticamente una base de conocimiento desde Google Drive y la API de GitHub
- **Sesiones Anónimas** — Tokens JWT con expiración configurable (24h por defecto)
- **Filtro Off-Topic** — Rechaza mensajes fuera del alcance del portafolio con respuestas amigables en español
- **Rate Limiting** — Protección contra abuso a nivel IP y por endpoint
- **Analytics con Redis** — Logging de consultas con hashes SHA-256 para privacidad (IP y session ID hasheados)
- **Deploy con Docker** — Stack completo de 3 servicios: Ollama + API + Nginx con TLS
- **OpenAPI** — Documentación automática de la API en modo desarrollo

---

## Arquitectura

```
┌─────────────────────────────────────────────────────────────────┐
│                         Nginx (TLS)                            │
│                     Puerto 80 → 443 (SSL)                      │
└────────────────────────────┬────────────────────────────────────┘
                             │ proxy_pass
┌────────────────────────────▼────────────────────────────────────┐
│                      ApiChatbot (.NET 10)                       │
│                                                                 │
│  ┌──────────┐   ┌──────────────────┐   ┌──────────────────┐   │
│  │  Session  │   │   ChatController │   │ SessionValidation │   │
│  │Controller │   │                  │   │   Middleware (JWT)│   │
│  └─────┬────┘   └────────┬─────────┘   └──────────────────┘   │
│        │                  │                                     │
│        └──────────┬───────┘                                     │
│                   ▼                                             │
│  ┌────────────────────────────────────┐                        │
│  │           ChatService              │                        │
│  │  ┌─────────────┐ ┌──────────────┐  │                        │
│  │  │OffTopicFilter│ │SessionStore  │  │                        │
│  │  └─────────────┘ │  (Memory)    │  │                        │
│  │                   └──────────────┘  │                        │
│  └────────────┬───────────────────────┘                        │
│               │                                                 │
│  ┌────────────▼──────┐  ┌──────────────────┐                   │
│  │ IChatProvider     │  │ IAnalyticsLogger │                   │
│  │ (Ollama)          │  │ (Redis)          │                   │
│  └────────┬──────────┘  └──────────────────┘                   │
│           │                                                     │
│  ┌────────▼────────────────────────────────┐                   │
│  │     Background Services                  │                   │
│  │  • ContextBuilderService (cada 24h)     │                   │
│  │  • OllamaWarmupService (al iniciar)     │                   │
│  └─────────────────────────────────────────┘                   │
└────────────────────────────┬────────────────────────────────────┘
                             │ HTTP
┌────────────────────────────▼────────────────────────────────────┐
│                       Ollama (LLM Local)                        │
│                  Puerto 11434 — qwen2.5:1.5b                   │
└─────────────────────────────────────────────────────────────────┘
```

### Flujo de una petición chat

1. El cliente envía `POST /api/v1/session` → recibe un JWT
2. El cliente envía `POST /api/v1/chat` con el token en `X-Session-Token`
3. El middleware JWT valida el token y extrae el `session_id`
4. `ChatService` verifica rate limiting, filtro off-topic y construye el prompt
5. Se envía el prompt + historial a Ollama vía `IChatProvider`
6. La respuesta se guarda en el historial de sesión
7. Se registra analytics en Redis (fire-and-forget, con IP y session hasheados)

---

## Requisitos Previos

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (para desarrollo local)
- [Docker y Docker Compose](https://docs.docker.com/get-docker/) (para despliegue)
- [Ollama](https://ollama.com/) (necesario si ejecutas sin Docker)

---

## Instalación Rápida

```bash
# 1. Clonar el repositorio
git clone https://github.com/tu-usuario/ApiChatbot.git
cd ApiChatbot

# 2. Crear el archivo .env desde la plantilla
cp .env.example .env
# Editar .env con tu configuración (ver Variables de Entorno)

# 3. Levantar el stack completo
docker compose up -d

# 4. Verificar que funcione
curl http://localhost:443/api/v1/session -X POST
```

---

## Variables de Entorno

Copia `.env.example` a `.env` y configura:

| Variable | Descripción | Valor por defecto |
|---|---|---|
| **Portfolio** | | |
| `Portfolio__Name` | Nombre del dueño del portafolio | `Tu Nombre` |
| `Portfolio__GithubUsername` | Usuario de GitHub | *(requerido)* |
| `Portfolio__SystemPrompt` | Prompt del sistema para el LLM | Ver `.env.example` |
| `Portfolio__ContextUrls__About` | URL del markdown "Acerca de mí" | *(requerido)* |
| `Portfolio__ContextUrls__Skills` | URL del markdown "Habilidades" | *(requerido)* |
| `Portfolio__ContextUrls__Projects` | URL del markdown "Proyectos" | *(requerido)* |
| **CORS** | | |
| `Cors__AllowedOrigins__0` | Primer origen permitido (producción) | `http://localhost:3000` |
| `Cors__AllowedOrigins__1` | Segundo origen permitido | `http://localhost:5173` |
| **Sesiones** | | |
| `Session__SecretKey` | Clave secreta para JWT (mín. 32 caracteres) | *(requerido)* |
| `Session__ExpiryHours` | Horas de vida del token | `24` |
| `Session__RateLimitPerMinute` | Límite de peticiones por minuto por sesión | `30` |
| **Ollama** | | |
| `Ollama__BaseUrl` | URL del servidor Ollama | `http://ollama:11434` |
| `Ollama__Model` | Modelo a utilizar | `qwen2.5:1.5b` |
| `Ollama__Temperature` | Temperatura del modelo (0.0 - 1.0) | `0.7` |
| `OLLAMA_MODEL` | Modelo que se descarga al iniciar el contenedor | `qwen2.5:0.5b` |
| **Analytics** | | |
| `Analytics__RedisConnection` | Host y puerto de Redis | *(opcional)* |
| `Analytics__TtlDays` | Días de retención de logs | `14` |
| **Entorno** | | |
| `ASPNETCORE_ENVIRONMENT` | Entorno de ejecución | `Development` |

---

## API Endpoints

### Crear Sesión

```http
POST /api/v1/session
```

**Response 200 OK:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2025-01-16T12:00:00Z"
}
```

### Enviar Mensaje

```http
POST /api/v1/chat
Content-Type: application/json
X-Session-Token: <token_de_la_sesión>

{
  "message": "¿Qué habilidades tiene?"
}
```

**Response 200 OK:**
```json
{
  "reply": "Victor tiene experiencia en..."
}
```

**Response 401 Unauthorized:** Token faltante, expirado o inválido
```json
{ "error": "Se requiere header X-Session-Token" }
```

**Response 429 Too Many Requests:** Rate limit excedido
```json
{ "error": "Demasiadas solicitudes. Intenta de nuevo en un momento." }
```

**Response 400 Bad Request:** Mensaje vacío o demasiado largo
```json
[
  { "propertyName": "Message", "errorMessage": "El mensaje no puede estar vacío" }
]
```

---

## Desarrollo Local

```bash
# Restaurar dependencias
dotnet restore

# Ejecutar en modo desarrollo
dotnet run --project ApiChatbot

# La API estará disponible en http://localhost:5236
# OpenAPI en http://localhost:5236/openapi/v1.json
```

### Pruebas con Bruno

El proyecto incluye una colección deBruno en `/bruno` para probar los endpoints:

1. Abrir Bruno y cargar la carpeta `bruno/`
2. Ejecutar **Create Session** → obtiene el token automáticamente
3. Ejecutar **Send Message** → usa el token de la sesión anterior

---

## Estructura del Proyecto

```
ApiChatbot/
├── Controllers/           # Endpoints REST
│   ├── ChatController.cs      # POST /api/v1/chat
│   └── SessionController.cs   # POST /api/v1/session
│
├── Domain/                # Lógica de negocio
│   ├── ChatService.cs         # Orquestación principal del chat
│   ├── IChatProvider.cs       # Interfaz para proveedores de IA
│   ├── OllamaChatProvider.cs  # Implementación para Ollama
│   ├── ISessionStore.cs       # Interfaz de almacenamiento de sesiones
│   ├── MemorySessionStore.cs  # Almacenamiento en memoria (ConcurrentDictionary)
│   ├── IAnalyticsLogger.cs    # Interfaz de analytics
│   ├── RedisAnalyticsLogger.cs # Logging en Redis
│   ├── OffTopicFilter.cs      # Filtro de mensajes fuera de tema
│   └── UrlContentFetcher.cs   # Descarga de contenido (con soporte Google Drive)
│
├── Models/                # DTOs de request/response
│   ├── ChatRequest.cs
│   ├── ChatResponse.cs
│   └── SessionResponse.cs
│
├── Validators/            # Validación de requests
│   └── ChatRequestValidator.cs # FluentValidation
│
├── Middleware/            # Middleware personalizado
│   └── SessionValidationMiddleware.cs # JWT validation
│
├── Background/            # Servicios en segundo plano
│   ├── ContextBuilderService.cs  # Reconstruye context.md cada 24h
│   └── OllamaWarmupService.cs    # Precarga el modelo al iniciar
│
├── Infraestructure/       # Configuración transversal
│   ├── DomainServicesConfig.cs   # Registro de DI
│   ├── MiddlewareConfig.cs       # Pipeline de middleware
│   ├── CorsConfig.cs             # Configuración CORS
│   ├── CorsExtensions.cs         # Extensión para aplicar CORS
│   ├── RateLimitConfig.cs        # Rate limiting
│   ├── SerilogConfig.cs          # Logging con Serilog
│   └── SwaggerConfig.cs          # Documentación OpenAPI
│
├── Program.cs             # Entry point
├── appsettings.json       # Configuración base
└── Dockerfile             # Multi-stage build para Docker
```

### Capas

| Capa | Responsabilidad |
|---|---|
| **Controllers** | Recibe HTTP, valida, delega al dominio |
| **Domain** | Lógica de negocio pura (chat, sesiones, IA, analytics) |
| **Models** | DTOs simples para transferencia de datos |
| **Validators** | Reglas de validación con FluentValidation |
| **Middleware** | Corte transversal (JWT, logging, rate limiting) |
| **Background** | Tareas programadas (context builder, warmup) |
| **Infrastructure** | Configuración de DI, CORS, rate limiting, logging |

---

## Tecnologías

### Stack Principal

| Tecnología | Versión | Uso |
|---|---|---|
| ASP.NET Core | 10.0 | Framework web |
| Ollama | latest | LLM local (qwen2.5) |
| Redis | latest | Analytics logging |
| Nginx | alpine | Reverse proxy con TLS |
| Docker Compose | v2 | Orquestación de servicios |

### NuGet Packages

| Paquete | Versión | Propósito |
|---|---|---|
| DotNetEnv | 3.2.0 | Carga de variables `.env` |
| FluentValidation | 12.1.1 | Validación de requests |
| Serilog | 4.3.1 | Structured logging |
| AspNetCoreRateLimit | 5.0.0 | Rate limiting por IP |
| StackExchange.Redis | 2.13.17 | Cliente Redis |
| System.IdentityModel.Tokens.Jwt | 8.19.1 | JWT creation/validation |
| Microsoft.AspNetCore.OpenApi | 10.0.2 | Documentación API |

---

## Docker

El stack está compuesto por 3 servicios:

| Servicio | Imagen | Puerto | Descripción |
|---|---|---|---|
| `ollama` | `ollama/ollama:latest` | 11434 (interno) | Servidor LLM local |
| `apichatbot` | `ApiChatbot/Dockerfile` | 8080 (interno) | API .NET |
| `nginx` | `nginx:alpine` | 80, 443 (externo) | Reverse proxy con TLS |

```bash
# Levantar todo
docker compose up -d

# Ver logs
docker compose logs -f apichatbot

# Parar
docker compose down
```

### Certificados TLS

- **Con Cloudflare:** Configura `CF_API_TOKEN` y `CF_DOMAIN` en `.env` para obtener certificados Origin CA automáticamente
- **Sin Cloudflare:** Se genera un certificado self-signed válido por 10 años

---

## Licencia

Creative Commons Attribution 4.0 International (CC BY 4.0) — ver [LICENSE](LICENSE) para más detalles.

Puedes usar, modificar y distribuir este proyecto, pero **siempre debes mencionar al autor original**.
