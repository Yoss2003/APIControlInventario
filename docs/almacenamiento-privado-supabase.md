# Almacenamiento privado de OmniStock

## Qué cambia

- Fotos y comprobantes de artículos: POST/GET/DELETE `/api/Articles/{id}/media`.
- Evidencia de custodias: POST/GET `/api/Assignments/{id}/evidence`.
- Solo la API tiene acceso a Supabase. La interfaz web usa las mismas rutas.
- PostgreSQL conserva referencias `sb:companies/ID/articles|assignments/...`, no URLs públicas.
- Los archivos antiguos guardados en `PrivateFiles` se pueden seguir leyendo
  desde el antiguo directorio **si todavía existen allí**. El cambio no los restaura.

## Configurar Supabase Storage

1. En Supabase > Storage, crea un bucket llamado `inventarioweb-private`.
2. Déjalo **privado** (desactiva "Public bucket").
3. Configura 10 MB como límite y acepta `image/jpeg`, `image/png`,
   `image/webp`, `application/pdf`.
4. En Supabase > Project Settings > API Keys, genera una clave **secret**
   `sb_secret_...` para uso exclusivo de InventoryAPI. Nunca usar la clave
   publicable en el servicio ni enviar esta clave a Angular.
5. En el directorio que contiene `InventoryAPI.csproj`:

   ```powershell
   dotnet user-secrets set "Supabase:Url" "https://TU-PROYECTO.supabase.co"
   dotnet user-secrets set "Supabase:Bucket" "inventarioweb-private"

   $secure = Read-Host "Clave secret de Supabase" -AsSecureString
   $plain = [System.Net.NetworkCredential]::new("", $secure).Password
   dotnet user-secrets set "Supabase:SecretKey" $plain
   Remove-Variable secure, plain
   ```

6. Inicia InventoryAPI normalmente. Si falta configuración, **las nuevas
   cargas fallarán explícitamente (HTTP 503)**; no escribirán en disco.

Para producción, define `Supabase__Url`, `Supabase__Bucket` y
`Supabase__SecretKey` mediante secretos del servidor (por ejemplo Render).
No coloques claves en appsettings.json o en GitHub.

## Trasladar archivos antiguos manualmente

Antes de esta operación asegúrate de que el servidor puede acceder al árbol
antiguo `PrivateFiles` de InventoryAPI. Si has movido la carpeta, configura
el directorio ORIGINAL que contiene `PrivateFiles`:

```powershell
dotnet user-secrets set "MediaMigration:LegacyRoot" "RUTA_A_TU_ANTIGUA_API"
dotnet run --launch-profile https -- --migrate-private-media
```

Si `PrivateFiles` está en el directorio actual de InventoryAPI, omite
`MediaMigration:LegacyRoot` y ejecuta solo el comando de migración.

- La migración recorre únicamente artículos y custodias de PostgreSQL.
- Copia cada archivo a Storage y solo después actualiza su referencia.
- Es idempotente: ignora referencias que ya empiezan por `sb:`.
- Reporta archivos antiguos que no puede encontrar; **no inventa ni borra**.
- Nunca borra los originales de disco automáticamente.
- Si el origen fue eliminado al reorganizar carpetas, el código no puede
  reconstruir la imagen que faltaba.

## Control de acceso

Todos los endpoints actuales requieren JWT de InventoryAPI y verifican
pertenencia a la empresa y permiso sobre artículo o custodia. La API usa
la clave secret de Supabase después de hacer esas verificaciones. El
bucket nunca debe configurarse como público.

## Fuera de este cambio

- Los logos de empresa ya se guardan como `bytea`/datos binarios en PostgreSQL.
- Las fotos antiguas de perfil de usuario todavía utilizan `wwwroot/images/profiles`
  y URL pública externa; necesitan ruta de lectura autenticada y ajuste
  específico de los clientes antes de migrarlas, para no romper pantallas.
- No ejecuta GitHub Actions, no crea migraciones de esquema de PostgreSQL
  y no requiere modificaciones en los endpoints Angular.
