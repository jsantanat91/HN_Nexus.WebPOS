# E2E Business Smoke

Script de humo E2E para validar flujo funcional base después de despliegue.

## Ejecutar

```powershell
powershell -ExecutionPolicy Bypass -File .\tests\e2e\business-smoke.ps1 -BaseUrl "https://tu-dominio" -Username "admin" -Password "tu-pass" -Tenant "t500"
```

## Qué valida

- Login con sesión web real.
- Acceso a dashboard.
- Acceso a venta.
- Acceso a historial de ventas.
- Acceso a monitoreo/salud.
- Acceso a tablero enterprise.

## Uso recomendado

- Ejecutarlo en cada despliegue (pipeline CD).
- Ejecutarlo también por tenant crítico (parametrizando `-Tenant`).
