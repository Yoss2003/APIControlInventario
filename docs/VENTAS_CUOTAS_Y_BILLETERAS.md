# Contrato API — Ventas en cuotas y billeteras digitales

La API conserva `PaymentType` numérico histórico (`Efectivo=0, Tarjeta=1, Yape=2, Plin=3, Bim=4, Transferencia=5, Cuotas=6`) para MAUI. Para el nuevo modal Angular también admite `paymentGroup` y `digitalWallet` como propiedades de entrada, sin cambiar columnas de la tabla `Sales`.

## Catálogo de pagos
`GET /api/Sales/payment-methods`

Devuelve los métodos principales, con `Billetera digital` agrupando Yape, Plin y Bim, y `Cuotas` con Diario, Semanal y Mensual. No exige un `paymentType` adicional cuando se envía `paymentGroup`.

## Venta con billetera digital
`POST /api/Sales` con JWT de usuario autorizado y cuerpo:

```json
{
  "paymentGroup": "BilleteraDigital",
  "digitalWallet": "Yape",
  "customerName": "Cliente",
  "saleDetails": [{ "articleId": 1, "quantity": 2, "unitPrice": 25 }]
}
```

La API resuelve el `PaymentType` definitivo a Yape (2). `digitalWallet` es obligatorio con `paymentGroup=BilleteraDigital`. El endpoint `POST /api/Sales/quick` también acepta `digitalWallet` opcional junto al `paymentMethod=BilleteraDigital`, además de las opciones heredadas.

## Crear venta a cuotas

`POST /api/Sales`

```json
{
  "paymentGroup": "Cuotas",
  "customerName": "Cliente",
  "saleDetails": [{ "articleId": 1, "quantity": 2, "unitPrice": 50 }],
  "installmentPlan": {
    "installmentCount": 3,
    "downPayment": 10,
    "frequency": "Mensual",
    "firstDueDate": "2026-12-10"
  }
}
```

`firstDueDate` es opcional (yyyy-MM-dd); por defecto se calcula a partir de la fecha de Perú: mañana/7 días/1 mes. Las cuotas dividen `totalAmount-downPayment` en céntimos exactos. Se permite entre 1 y 120 cuotas, montos positivos, dos decimales y cuota inicial menor al total. No se aceptan intereses implícitos.

La creación de venta, los movimientos de stock y los registros de cuotas son atómicos en una transacción. La cuota inicial se guarda en `Sale.AmountReceived`, que después acumula los abonos confirmados. Las cuotas se guardan en `SaleInstallments` y sus abonos en `SaleInstallmentReceipts`. Los módulos heredados de `AccountsReceivables` e `InstallmentPayments` no se modifican.

## Venta a cuotas desde el POS rápido

`POST /api/Sales/quick` también admite el cronograma. Conserva su `operationKey` para impedir ventas duplicadas y permite artículos serializados con `detailIds`:

```json
{
  "articleId": 1,
  "quantity": 1,
  "unitPrice": 120,
  "customerName": "Cliente identificado",
  "paymentMethod": "Cuotas",
  "notes": "Crédito",
  "operationKey": "9ff41924-5c8c-46aa-8ab1-ccfd6b87b64b",
  "detailIds": [],
  "installmentFrequency": "Mensual",
  "installmentCount": 3,
  "downPayment": 30,
  "firstDueDate": "2026-12-10"
}
```

El campo `firstDueDate` es opcional. Para `Cuotas` es necesario identificar al cliente. La venta rápida y el endpoint general comparten el cálculo del cronograma y registran las cuotas con la misma transacción de stock.

## Simulación antes de guardar

`POST /api/Sales/installments/simulate`:

```json
{
  "totalAmount": 100,
  "installmentPlan": {
    "installmentCount": 3,
    "downPayment": 10,
    "frequency": "Mensual"
  }
}
```

Devuelve vencimientos e importes calculados con la misma función usada para persistir, sin crear una venta.

## Consultar cronograma y saldos

`GET /api/Sales/{saleId}/installments`

Devuelve `totalAmount`, `downPayment`, `financedAmount`, `balance`, `cancelled` e `installments`, con estado `Pending`, `Partial`, `Overdue`, `Paid` o `Cancelled`, y el historial de abonos. Requiere acceso de lectura al inventario correspondiente.

## Registrar abono

`POST /api/Sales/{saleId}/installments/{installmentId}/payments`

```json
{
  "amount": 20,
  "paymentMethod": "BilleteraDigital",
  "digitalWallet": "Plin",
  "notes": "Abono confirmado",
  "operationKey": "7a5e3190-c611-41cb-8c43-8d01fe1fb50b"
}
```

`operationKey` debe ser un UUID nuevo para cada abono; reenviar la misma clave/importe/cuota/método devuelve `reused=true` y no lo duplica. La API bloquea la fila de la cuota para evitar sobreabonos concurrentes y prohíbe abonar ventas anuladas. No acepta `Cuotas` como medio de abono. Requiere permiso de escritura para los artículos vendidos.

## Reglas de anulación y compatibilidad

La anulación de una venta a cuotas con cuota inicial o abonos ya registrados devuelve `409 Conflict` hasta que exista un proceso explícito de reembolso. Una venta a cuotas todavía sin cobros puede anularse y su cronograma queda visible como `Cancelled`.

Los dos endpoints de venta aceptan `Cuotas` cuando incluyen una configuración de financiamiento válida; `quick` utiliza los campos `installmentCount`, `downPayment` e `installmentFrequency`, mientras `POST /api/Sales` utiliza `installmentPlan`.

La migración `20261111043000_SaleInstallmentLedger` crea dos tablas nuevas y sus índices. El arranque actual de la API ejecuta `Database.Migrate()`, por lo que la migración se aplicará al iniciar el código después del despliegue: hacer copia de seguridad de PostgreSQL previamente.

## Compatibilidad financiera y protecciones tomadas de PR #30

Solo existe **un sistema para las ventas a cuotas nuevas**: `SaleInstallments` y `SaleInstallmentReceipts`. El catálogo y el control de billeteras son compartidos por ventas normales, crédito y abonos. La migración es aditiva y no elimina tablas ni registros históricos.

Para conservar datos previos, `AccountReceivables` e `InstallmentPayments` siguen estando disponibles como **histórico**. Las operaciones `GET` comprueban pertenencia a la empresa y acceso al inventario. Las rutas `POST/PUT/DELETE /api/AccountReceivables` se cierran para evitar la creación, modificación o borrado manual de deudas. Las rutas `PUT/DELETE /api/InstallmentPayments` se cierran para evitar modificar los abonos registrados.

`POST /api/InstallmentPayments` se conserva únicamente para pagos de créditos **históricos comprobados**: valida relación movimiento-venta, saldo, permisos, total recibido y anulación; bloquea primero venta y luego cuota. Si la venta tiene un cronograma en el libro nuevo, rechaza el cobro para no duplicar saldos. Este endpoint histórico no dispone de clave idempotente: no se debe reintentar un cobro sin consultar primero el resultado. Los cobros nuevos deben usar el endpoint idempotente de `Sales`.

El antiguo `GET /api/InstallmentPayments/sale/{id}` sigue sirviendo consultas de créditos históricos; los nuevos usan `GET /api/Sales/{id}/installments`.

No se puede anular un crédito nuevo si tuvo adelanto o abonos. Un crédito nuevo sin cobros se puede anular y su cronograma se conserva para auditoría. Un crédito histórico sin cronograma del libro nuevo se deriva a revisión antes de anular; esto evita restablecer stock sin reconciliar obligaciones previas.

**PR #30 y #31 no deben fusionarse juntas.** Las protecciones de la #30 ya fueron integradas en #31 adaptadas al libro nuevo. El frontend Angular debe consumir los endpoints nuevos antes del despliegue.

## Comprobaciones antes de producción

- Construir: `dotnet build InventoryAPI.sln` con .NET 10 y PostgreSQL de pruebas.
- Simular 3 cuotas de 100 - 10 = 90; verificar fechas e importes.
- Simular 3 cuotas de 100 - 0 = 100; verificar reparto 33.34 + 33.33 + 33.33.
- Crear una venta válida y verificar stock, movimientos, suma de cuotas y la migración.
- Rechazar cuota inicial >= total, cuotas 0, frecuencia inválida, billetera sin seleccionar.
- Abonar parcialmente y totalmente; comprobar saldo y estado en GET.
- Repetir misma `operationKey` y disparar dos abonos concurrentes para verificar idempotencia y bloqueo.
- Verificar que usuarios de otra empresa y con permisos de solo lectura no puedan acceder o abonar.
- Verificar anulación con pagos pendientes y sin pagos.
- Verificar que las ventas normales y el POS rápido mantengan el funcionamiento previo.

**Nota:** la implementación del ComboBox en Angular queda fuera de este repositorio. El repositorio Angular no está disponible entre los repositorios conectados en esta sesión.
