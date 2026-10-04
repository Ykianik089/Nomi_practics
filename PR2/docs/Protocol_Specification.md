# Спецификация протокола Nomi (фрагмент PR2)

## Формат датаграммы
Header (12 байт) + Payload

| Поле | Размер | Тип |
|---|---:|---|
| Version | 1 | uint8 |
| Type | 1 | uint8 |
| Sequence | 2 | uint16 BE |
| PayloadSize | 2 | uint16 BE |
| Reserved | 2 | uint16 BE |
| Checksum | 4 | uint32 BE (CRC32 payload) |

## PING (Type = 17)
| Поле | Размер |
|---|---:|
| ClientSendTimestampMs | 8 (int64 BE) |
| Nonce | 4 (uint32 BE) |

## PONG (Type = 18)
| Поле | Размер |
|---|---:|
| ClientSendTimestampMs | 8 |
| ServerReceiveTimestampMs | 8 |
| Nonce | 4 |

RTT вычисляется на клиенте:
`RTT = now_ms - ClientSendTimestampMs`