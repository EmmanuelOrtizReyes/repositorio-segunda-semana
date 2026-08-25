# Refactoring Challenge — Semana 2

API REST de pedidos para practicar calidad de código, diseño mantenible, refactorización y pruebas sin alterar el comportamiento observable.

## Contrato funcional

- Un pedido requiere nombre, correo con `@` y entre 1 y 10 líneas de artículos.
- Cada cantidad debe estar entre 1 y 20, ambos límites incluidos.
- Los SKU disponibles al iniciar son `BOOK` (25.00, stock 30), `KEYBOARD` (50.00, stock 12) y `HEADPHONES` (80.00, stock 8).
- No se crea un pedido si un producto no existe o no dispone de stock suficiente.
- `SAVE10` descuenta 10% cuando el subtotal es al menos 100.00.
- `VIP20` descuenta 20% cuando el subtotal es al menos 200.00.
- Un cupón reconocido que no alcance su mínimo no aplica descuento. Un cupón desconocido es inválido.
- El envío cuesta 12.50; es gratuito cuando el subtotal es al menos 150.00.
- Un pedido nuevo queda `Pending` y descuenta el stock solicitado.
- Solo un pedido `Pending` puede confirmarse. Confirmarlo cambia su estado a `Confirmed` y envía una notificación.
- Los correos terminados en `@fail.test` simulan un fallo del proveedor durante la confirmación y producen HTTP 502.
- Un pedido pendiente puede cancelarse y su stock se repone. Repetir la cancelación conserva el estado y no repone stock otra vez.
- Un pedido confirmado no puede cancelarse.

## Endpoints

| Método | Ruta | Entrada | Resultado | Códigos relevantes |
|---|---|---|---|---|
| GET | `/api/orders` | Ninguna | Lista de pedidos | 200 |
| GET | `/api/orders/{id}` | Id en ruta | Pedido solicitado | 200, 404 |
| POST | `/api/orders` | JSON con `customerName`, `customerEmail`, `items` y `couponCode` opcional | Pedido creado | 201, 400, 409 |
| POST | `/api/orders/{id}/confirm` | Id en ruta | Pedido confirmado | 200, 404, 409, 502 |
| POST | `/api/orders/{id}/cancel` | Id en ruta | Pedido cancelado | 200, 404, 409 |

Ejemplo de creación:

```json
{"customerName":"Ana López","customerEmail":"ana@example.com","items":[{"sku":"BOOK","quantity":2},{"sku":"KEYBOARD","quantity":1}],"couponCode":"SAVE10"}
```

## Ejecución

```bash
dotnet restore
dotnet build
dotnet run --project src/RefactoringChallenge.Api
```
