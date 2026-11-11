# POS: ventas a cuotas y registro de abonos

Implementación de API con los modelos existentes `Sale`, `Movement`, `AccountReceivable` e `InstallmentPayment`. No requiere migración nueva.

## Crear venta a cuotas

`POST /api/Sales/quick` (JWT con acceso de escritura al inventario)

```json
{
  "articleId": 1,
  "quantity": 1,
  "unitPrice": 120,
  "customerName": "Cliente identificado",
  "paymentMethod": "Cuotas",
  "notes": "Crédito acordado",
  "operationKey": "8facfdd5-96cb-4132-adf9-6f4eeaeea123",
  "detailIds": [],
  "installmentFrequency": "Mensual",
  "installmentCount": 3,
  "downPayment": 30
}
```

Para este ejemplo se registra el adelanto de S/ 30 y tres cuotas por S/ 30, venciendo en los próximos meses.

Reglas del backend:
- Frecuencia `Diario`, `Semanal` o `Mensual`; entre 1 y 60 cuotas.
- Comprador identificado; no se permite `Público general`.
- Adelanto `>= 0`, menor al total, hasta 2 decimales. El saldo debe cubrir al menos S/ 0.01 por cuota.
- El importe financiado se divide en céntimos; la última cuota absorbe el residuo de redondeo.
- Fechas de vencimiento en formato `yyyy-MM-dd`, calculadas a partir de la fecha UTC de la venta.
- Venta, stock, movimiento, cronograma y clave idempotente se guardan en **una transacción**.
- `Sale.AmountReceived` empieza con el adelanto (no con el total).
- Billeteras digitales se siguen enviando como `Yape`, `Plin` o `Bim`: la API no acepta literalmente `Billetera digital`.

## Consultar el crédito

`GET /api/InstallmentPayments/sale/{saleId}` devuelve total, cobrado, saldo, cuotas, estados y abonos.

`GET /api/AccountReceivables` y `GET /api/InstallmentPayments` conservan la consulta histórica. Las escrituras directas sobre cuentas y la edición/borrado de abonos se bloquean para evitar saldos incoherentes.

## Registrar un abono

`POST /api/InstallmentPayments`:

```json
{
  "installmentId": 123,
  "paymentAmount": 10.50,
  "observation": "Abono en caja"
}
```

El backend comprueba permisos de escritura, venta activa a cuotas, saldo existente, importe con dos decimales y consistencia de abonos previos. Bloquea la venta y la cuota, agrega `InstallmentPayment`, actualiza `AccountReceivable.PaidAmount` / `Status` y suma el cobro a `Sale.AmountReceived`, en una transacción.

Un abono no se puede editar ni borrar: falta implementar una operación auditada de reversión/refund. No reintentes automáticamente un POST de cobro sin verificar su resultado en la API: todavía no hay clave idempotente individual para cada abono.

## Anulación

`POST /api/Sales/{saleId}/cancel`: las ventas a cuotas con algún adelanto o abono **no** se pueden anular hasta implementar su devolución financiera. Una venta a cuotas sin cobros sí puede anularse: recupera el stock y sus cuentas quedan `Cancelled`.

Los demás métodos de pago conservan su flujo de ventas y cancelaciones.

## Pruebas recomendadas antes de combinar

1. Crear un crédito con total S/ 100.00, adelanto S/ 10.00 y 3 cuotas: esperar S/ 30.00 cada una.
2. Crear un crédito con saldo S/ 100.00 y 3 cuotas: esperar S/ 33.33, 33.33 y 33.34.
3. Consultar el cronograma, abonar S/ 10.00 en la cuota 1, confirmar saldo S/ 23.33, y completar la cuota.
4. Rechazar un abono de mayor importe que el saldo, un abono negativo y valores con tres decimales.
5. Intentar anular la venta después de cobrar: debe devolver conflicto y no modificar el stock.
6. Anular una venta a cuotas sin adelanto ni abonos: cancelar cuentas y devolver stock.
7. Verificar que otros usuarios/empresas no pueden consultar o modificar los cobros de inventarios sin permiso.
8. Repetir `POST /api/Sales/quick` con el mismo `operationKey`: no debe crear otra venta.
9. Probar contado, Yape, Plin, Bim y venta serializada para comprobar que continúan operativos.

**Importante:** las cuotas quedan listas en la API, pero la UI Angular debe enviar los parámetros y consultar el crédito. Si tu frontend todavía tiene `creditApiReady = false`, actívalo únicamente tras desplegar y validar esta versión de la API.
