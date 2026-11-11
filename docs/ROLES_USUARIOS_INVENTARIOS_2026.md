# Roles, usuarios e inventarios — contrato de desarrollo (noviembre 2026)

## Modelo conservado

- **Inventario maestro**: uno por empresa, creado automáticamente por `InventoryProvisioner.NewMaster`. `IsMaster=true`, `UserId=0`. Guarda catálogos compartidos; nunca stock operativo.
- **Inventario personal**: creado al registrar un usuario, `IsMaster=false`, `UserId` del propietario; guarda artículos y stock.
- **Inventario adicional**: solo con permiso `CREATE_INVENTORIES`; nunca se convierte en maestro.
- **Inventario compartido**: `SharedInventories` controla lectura/edición, con `CompanyId` coincidente.
- **Empresa**: en toda operación normal se toma del JWT; nunca se confía en `CompanyId` recibido del navegador.
- **Desarrollador** (rol 1): consulta global de maestros; las operaciones de stock requieren contexto de empresa explícito. Alta del primer administrador por ruta exclusiva.
- **Administrador** (rol 2): maestro de su empresa, inventarios personales y gestión de usuarios e inventarios de su empresa.
- **Otros roles**: permisos existentes `RolePermissions` (códigos `Permission.SystemCode`). El usuario solo trabaja sobre lo propio/compartido, según permisos.

## Códigos añadidos

| Código | Uso |
|---|---|
| `VIEW_MASTER_INVENTORY` | Consultar el maestro de la empresa |
| `MANAGE_MASTER_CATALOG` | Crear/editar catálogos de la empresa |
| `CREATE_INVENTORIES` | Crear, renombrar y desactivar inventarios operativos |
| `MANAGE_INVENTORY_ACCESS` | Gestionar acceso a inventarios |
| `MANAGE_USERS` | Crear y administrar usuarios de la empresa |

El catálogo se siembra idempotentemente sin borrar permisos preexistentes. Roles 1 y 2 conservan capacidades implícitas para compatibilidad. **Solo el desarrollador puede cambiar la matriz de permisos de los roles 3+**, pues los roles son globales y afectan a todas las empresas. Los roles 1/2 están protegidos.

## Endpoints

| Método y ruta | Acceso |
|---|---|
| `GET /api/Access/me` | Permisos efectivos, empresa del JWT y maestro activo |
| `GET /api/Inventories/masters` | Desarrollador: todos los maestros; demás con permiso: solo su empresa |
| `GET /api/Inventories/masters/{id}/catalogs` | Solo lectura; cruce de empresas únicamente para desarrollador |
| `GET /api/Inventories` | Maestro autorizado y operativos visibles en la empresa del JWT |
| `POST /api/Inventories` | `CREATE_INVENTORIES`, propietario de la misma empresa |
| `POST /api/Inventories/assign` | Desarrollador/administrador, permisos de creación y acceso, propietario de la misma empresa |
| `POST /api/Inventories/Share` | `MANAGE_INVENTORY_ACCESS`, origen autorizado; destino de la misma empresa |
| `POST /api/dev/companies/{id}/first-admin` | Solo desarrollador; primer administrador de la empresa; genera personal atómicamente |
| `GET /api/UserManagement/users` | `MANAGE_USERS`, lista limitada a empresa del JWT |
| `POST /api/UserManagement/users` | `MANAGE_USERS`; rol no privilegiado; genera personal atómicamente |
| `PATCH /api/UserManagement/users/{id}/details` | `MANAGE_USERS`, empresa del JWT |
| `PATCH /api/UserManagement/users/{id}/access` | `MANAGE_USERS`; no autoedición ni roles 1/2 |
| `GET/PUT /api/UserManagement/users/{id}/permissions` | Consulta propia o gestión; edición de flags por roles de administración |
| `POST /api/Roles/{id}/permissions` | Solo desarrollador, roles 3+ |

### Registro del primer administrador

```http
POST /api/dev/companies/7/first-admin
Authorization: Bearer TOKEN_DESARROLLADOR
Content-Type: application/json
```

```json
{
  "username": "admin-grifo",
  "email": "admin@ejemplo.com",
  "firstName": "Administración",
  "lastName": "Grifo",
  "initialPassword": "CONTRASENA-FUERTE-PRUEBA",
  "dni": null,
  "roleId": 2
}
```

La respuesta contiene `personalInventoryId`. **No** devuelve el hash ni la contraseña. El formulario debe recoger la clave directamente de la persona responsable y compartirla por un canal privado. Las cuentas gestionadas por el nuevo flujo se activan sin correo de aprobación; no se utiliza el enlace legado de aprobación pública.

## Validación manual imprescindible (base local)

1. Con tu desarrollador, `GET /api/Inventories/masters` debe incluir `CompanyId=7`, maestro `Id=3`.
2. Con un empleado de empresa 7, `GET /api/Inventories/masters` debe ser 403 si no tiene `VIEW_MASTER_INVENTORY`; si lo tiene, no debe listar empresas 1, 2, 3, etc.
3. `POST /api/dev/companies/7/first-admin`: una sola alta exitosa; segundo intento devuelve conflicto y no duplica usuarios ni inventarios.
4. `SELECT "Id","InventoryName","UserId","IsMaster","IsActive" FROM "Inventories" WHERE "CompanyId"=7 ORDER BY "Id";` debe mostrar maestro 3 más el personal del primer administrador.
5. Login como administrador de empresa 7: debe ver solo maestro de empresa 7 y operativos de su empresa; usuario de otra empresa nunca debe leer inventario 3 con `GET /api/Inventories/3` (404).
6. `POST /api/Inventories` con usuario sin `CREATE_INVENTORIES` -> 403. Con permiso, crea operativo no maestro y de propietario de la misma empresa.
7. `POST /api/Inventories/assign` con propietario de otra empresa -> 404. No debe persistir un inventario.
8. Usuario delegado con `MANAGE_USERS` pero sin otros privilegios no puede elevar otros a roles superiores a los suyos.
9. No se puede editar `RoleId 1` ni conceder permisos de roles 1/2 desde la API.
10. Repetir con dos empresas y revisar el rol y CompanyId de los JWT; probar cambios de estado, compartir y revocar.

## Límites de esta entrega

- La CI compila la API y Angular, **no ejecuta pruebas contra tu PostgreSQL local**; debes confirmar los escenarios antes de considerar completada la migración.
- Los permisos específicos de negocio de `EmployeePermissions` (descuentos, anulación, costos, ganancias) todavía requieren revisión de aplicación transversal en **todos** los controladores respectivos; no confundir con los permisos estructurales ya protegidos.
- El alta vieja `POST /api/Users` se conserva por compatibilidad, con autorización y bloqueo de roles 1/2; para nuevas altas Angular usa el flujo administrado, sin correos con contraseña.
- `GET /api/Users/ApproveAccount/{id}` público queda desactivado (410). Se requiere aprobación autenticada por `POST` para altas antiguas pendientes. No reutilizar los enlaces antiguos.
- Rol y usuario existentes no se eliminan, no se reasignan automáticamente a otra empresa y no se crean maestros duplicados.
