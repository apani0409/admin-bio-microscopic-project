# CLAUDE.md — Colección de microscopía (UCR · Escuela de Biología)

Contexto compartido para Claude Code. Todo el equipo trabaja con este archivo en la raíz del repo, así que mantenerlo corto y actualizado.

## Proyecto

Curso CI-0139 Administración de Proyectos (UCR/ECCI, G1, II-2026). Proyecto: **Mejora en las opciones de visualización de imágenes de la colección de microscopía de la Escuela de Biología**.

Equipo: cuatro integrantes (una persona PM y tres de desarrollo). La asignación por persona se maneja fuera de este repo público.

### Qué exige el Acta de Constitución (v1.0, 4-sep-2026)

Objetivos:
1. Consulta en línea de las láminas sin depender de la persona administradora.
2. Un solo repositorio con varias vistas por lámina.
3. Navegación y búsqueda por grupos, cajas, láminas y códigos.
4. La persona administradora solo carga y cura el contenido.
5. Vista protegida (acceso controlado a las imágenes).

Dentro del alcance: plataforma en línea desplegada en el servidor de la Escuela de Biología, vista protegida y control de acceso, búsqueda/navegación, documentación de uso.

Fuera del alcance: digitalización, préstamos, hardware, apps móviles nativas, contenido didáctico, mantenimiento posterior al semestre, **integración con sistemas institucionales UCR (no hay SSO UCR)**.

Restricciones: la estructura de la Escuela es fija, no hay presupuesto, las condiciones del servidor son desconocidas.

Ambigüedades abiertas (confirmar con las personas patrocinadoras antes de cerrar el diseño): qué significa exactamente "vista protegida" y cómo se autoriza a docentes/estudiantes. Mientras tanto, asumir dos roles: **Administrador** (CRUD completo) y **Lector** (solo consulta, con sesión).

## Estado actual del código (verificado)

- Identity con cookie y roles `Administrador` y `Lector`. Todo el API está bajo `/api`; las lecturas exigen sesión y las escrituras, `Administrador`.
- Las escrituras exigen el header `X-Requested-With: BioMicroscope`.
- Swagger solo en Development.
- Las imágenes se guardan en `Storage:Path` y se sirven por `/api/images/{id}/file` y `/thumbnail` con sesión.
- El API sirve el front Blazor en el mismo origen. No hay CORS.
- La cadena de conexión de `postgres/postgres` existe solo en `appsettings.Development.json`.
- Los tests de integración usan Testcontainers y requieren Docker.

## Stack y mapa del repo

.NET 9, Clean Architecture, EF Core + PostgreSQL 17, Blazor WASM + MudBlazor, cliente HTTP escrito a mano (`BioMicroscopeApiClient`).

- `Backend.Domain` · `Backend.Application` · `Backend.Infrastructure` · `Backend.Presentation` (endpoints minimal API en `Endpoints/`) · `Backend.DependencyInjection` · `Backend.Api` (host).
- `Frontend` (host WASM) · `Frontend.Domain` · `Frontend.Application` · `Frontend.Infrastructure` (cliente HTTP en `ApiClient/`) · `Frontend.Presentation` (páginas MudBlazor) · `Frontend.DependencyInjection`.
- Un proyecto `*.Tests.Unit` por capa.
- Entidades: Group → Box → Slide → Image (la lámina tiene varias imágenes con orden).
- Páginas: Login, Home, Groups, Boxes, Slides, SlideDetail, Users, GuiaUso.

## Comandos

```bash
docker compose up -d                       # Postgres (127.0.0.1:5432, db biomicroscope_admin)
dotnet run --project Backend.Api --launch-profile http  # App y API en http://localhost:5138
dotnet test UCR.EB.BioMicroscopeAdmin.sln  # todos los tests (Docker encendido)
docker compose -f docker-compose.prod.yml up -d --build  # despliegue completo (requiere .env)
```

## Decisiones técnicas

1. **Autenticación por cookie, mismo origen.** ASP.NET Core Identity + roles (`Administrador`, `Lector`). Motivo: un `<img src>` no puede mandar un header `Authorization`, así que JWT bearer rompería la visualización de imágenes protegidas.
2. **Imágenes fuera de `wwwroot`.** Guardarlas en una carpeta configurable (`Storage:Path`, volumen en Docker) y servirlas por un endpoint con `RequireAuthorization()`. Sin `UseStaticFiles` para ellas.
3. **Política por defecto = autenticado.** Lecturas: cualquier usuario con sesión. Escrituras (POST/PUT/DELETE): solo `Administrador`.
4. **Swagger solo en Development.** La pestaña "Documentación API" solo aparece en ese entorno.
5. **Un solo despliegue en contenedor** (API + front estático + volumen de imágenes + Postgres) con `Dockerfile` y `docker-compose.prod.yml`. Es lo que se puede mover al servidor de la Escuela.
6. **Secretos fuera del repo.** Cadena de conexión y contraseña del admin inicial por variables de entorno.
7. **Un solo origen también en desarrollo.** El API hospeda el front en el puerto 5138 para que la cookie funcione sin CORS.

## Reglas de colaboración

- El repo original pertenece a la persona PM. Trabajamos en un **fork** (no copiar el código a un repo nuevo, para poder abrir PR hacia el original sin permisos de escritura).
- Una rama por paquete: `feat/<paquete>-<tema>`. PR pequeños hacia `main` del fork; al menos una persona revisa antes de mezclar.
- **Migraciones EF:** las crea solo quien toca el modelo, y se rebasa antes de crear otra. Si dos ramas crean migraciones, se borra la propia y se regenera sobre `main` (no mezclar `AppDbContextModelSnapshot.cs` a mano).
- Cada paquete trae sus tests en el proyecto `*.Tests.Unit` de la capa que toca.
- No subir `wwwroot/uploads`, `build/`, `Electron/dist` ni secretos.
- Cualquier cambio de alcance (no solo de código) pasa por el Plan de Gestión de Cambios del curso.

## Paquetes de trabajo (reparto propuesto, ajustar en equipo)

| # | Paquete | Qué incluye | Responsable | Depende de |
|---|---------|-------------|-----------|-----------|
| 0 | Esqueleto | Fork, este archivo, README con cómo correr, `docker compose`, rama `main` verde | Dev 1 | — |
| 1 | Auth backend | Identity + cookie, roles, usuario admin inicial por variable de entorno, política por defecto, Swagger solo en Dev, endpoints `/auth/login`, `/auth/logout`, `/auth/me` | Dev 1 | 0 |
| 2 | Login en el front | Página de login, `AuthenticationStateProvider`, rutas protegidas, ocultar botones de admin al Lector, cerrar sesión | Dev 2 | contrato de 1 (se puede avanzar con un mock) |
| 3 | Imágenes protegidas | Almacenamiento fuera de `wwwroot`, endpoint autorizado, miniaturas, visor con varias vistas por lámina | Dev 3 | 1 |
| 4 | Gestión de usuarios | Pantalla del Administrador para crear/desactivar usuarios Lector | Dev 2 | 1, 2 |
| 5 | Despliegue y docs | `Dockerfile`, `docker-compose.prod.yml`, prueba en el host de demo, README, actualizar `GuiaUso`, lista de aceptación contra el Acta | PM + quien termine primero | 1, 3 |

El paquete 1 desbloquea a todos: mezclarlo primero (aunque sea con lo mínimo) y publicar el contrato de los endpoints de auth para que 2 y 3 avancen en paralelo.

## Prompt para arrancar la Fase 0

> Lee CLAUDE.md. Haz solo el paquete 0: confirmar que el repo compila y que `dotnet test` pasa, escribir un README real (cómo levantar BD, API y front), y dejarlo en una rama `feat/esqueleto`. No toques autenticación todavía. Al final dime qué falló o qué supuestos tuviste que hacer.
