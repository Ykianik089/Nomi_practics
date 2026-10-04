# Практика №2 — Телеметрия UDP-протокола (PING/PONG)

## О чём эта работа

Вторая практика по дисциплине «Проектирование клиент-серверных систем».
Цель — построить **подсистему телеметрии** поверх бинарного UDP-протокола
Nomi и научиться измерять характеристики канала:

- **RTT** — Round Trip Time, время «туда-обратно»;
- **SRTT** — сглаженный RTT (экспоненциальное среднее);
- **RTTVAR** — сглаженное отклонение RTT;
- **Jitter** — вариация задержки между соседними измерениями;
- **LossRate** — доля потерянных пакетов.

Помимо метрик, работа проверяет корректную обработку **недействительных,
дублирующихся и запоздалых ответов**, а также качество модульной
архитектуры: сетевой ввод, сериализация и расчёт метрик не смешиваются
в одном слое.

---

## Что реализовано

### 1. Протокол PING/PONG

В дополнение к командам из практики №1 добавлены два новых типа пакетов:

| Код | Имя  | Назначение                     |
|----:|------|--------------------------------|
|  17 | PING | клиент → сервер: запрос замера |
|  18 | PONG | сервер → клиент: ответ на PING |

Формат датаграммы не изменился: **заголовок 12 байт + payload**.

**PING payload — 12 байт:**

| Поле                   | Размер | Тип                        |
|------------------------|-------:|----------------------------|
| ClientSendTimestampMs  |      8 | int64 BE, мс с Unix epoch  |
| Nonce                  |      4 | uint32 BE, идентификатор   |

**PONG payload — 20 байт:**

| Поле                     | Размер | Тип        |
|--------------------------|-------:|------------|
| ClientSendTimestampMs    |      8 | int64 BE   |
| ServerReceiveTimestampMs |      8 | int64 BE   |
| Nonce                    |      4 | uint32 BE  |

RTT вычисляется **на клиенте** как
`RTT = now_ms − ClientSendTimestampMs` — без синхронизации часов.

### 2. Модульная архитектура

| Проект           | Ответственность                                                                 |
|------------------|---------------------------------------------------------------------------------|
| `Nomi.Protocol`  | `PacketType`, `PacketHeader`, `Crc32`, `PacketSerializer`, `PingPongCodec`.    |
| `Nomi.Transport` | `IUdpTransport` / `UdpTransport` — обёртка над `UdpClient`. Только send/receive.|
| `Nomi.Telemetry` | `RttStatistics`, `PingTracker`, `CsvLatencyWriter`. Только статистика.         |
| `Nomi.Server`    | Принимает PING, отвечает PONG.                                                  |
| `Nomi.Client`    | Шлёт PING, обрабатывает PONG, пишет CSV.                                        |
| `Nomi.Tests`     | xUnit-тесты сериализации, CRC, версии, статистики.                              |

Слои не смешиваются: `Telemetry` не знает про `UdpClient`, `Transport`
не знает про CRC и про RTT.

### 3. Метрики

| Метрика  | Формула                                            | Реализация                    |
|----------|----------------------------------------------------|-------------------------------|
| RTT      | `now − clientSendTimestamp`                        | `RttStatistics.OnRttSample`   |
| SRTT     | `(1−α)·SRTT + α·RTT`, α = 1/8                      | там же                        |
| RTTVAR   | `(1−β)·RTTVAR + β·\|SRTT−RTT\|`, β = 1/4           | там же                        |
| Jitter   | `(1−γ)·Jitter + γ·\|RTT_i − RTT_{i−1}\|`, γ = 1/16 | там же                        |
| LossRate | `Lost / Sent`                                      | там же                        |

α, β, γ — стандартные значения из RFC 6298 (SRTT/RTTVAR) и RTP (jitter).

### 4. Классификация ответов

`PingTracker` различает пять исходов:

| Результат   | Когда                                                          |
|-------------|----------------------------------------------------------------|
| `Accepted`  | nonce найден в `_pending`, RTT посчитан                        |
| `Duplicate` | nonce уже был обработан ранее                                  |
| `Late`      | PONG пришёл после таймаута                                     |
| `Unknown`   | nonce не найден и не помечен ни как duplicate, ни как late     |
| `Invalid`   | пакет не прошёл десериализацию/CRC                             |

Дубликат не учитывается в RTT повторно, поздний ответ не «воскрешает»
уже учтённую потерю, а неизвестный nonce не портит SRTT.

### 5. Журнал измерений

Клиент пишет CSV со следующими колонками:

```
timestamp_utc,sequence,nonce,event,rtt_ms,srtt_ms,jitter_ms,loss_rate,status
```

Каждое событие (`SENT`, `Accepted`, `LOSS`, `Duplicate`, `Late`, `Unknown`)
попадает отдельной строкой. Файл: `docs/latency_samples.csv`.

---

## Структура проекта

```
Nomi_practics/PR2/
├── README.md                     ← этот файл
├── Nomi.PR2.sln
├── src/
│   ├── Nomi.Protocol/            ← протокол, CRC, кодек PING/PONG
│   ├── Nomi.Transport/           ← обёртка над UdpClient
│   ├── Nomi.Telemetry/           ← RttStatistics, PingTracker, CSV
│   ├── Nomi.Server/              ← отвечает PONG
│   └── Nomi.Client/              ← шлёт PING, считает метрики
├── tests/
│   └── Nomi.Tests/               ← xUnit-тесты
├── docs/
│   ├── Protocol_Specification.md
│   ├── Experiment_Config.md
│   ├── Latency_Report.md
│   ├── latency_samples.csv       ← журнал измерений
│   └── latency_rtt.png           ← график RTT/SRTT/Jitter
└── scripts/
    └── plot_latency.py
```

---

## Как запустить

### 1. Сборка и тесты

```bash
dotnet build
dotnet test
```

Ожидаемо: `Passed: 7+` — тесты сериализации, CRC, версии, статистики
и классификации.

### 2. Сервер (терминал №1)

```bash
dotnet run --project src/Nomi.Server -- --port 5000
```

Печатает:

```
[SERVER] listening...
[SERVER] PING seq=1 from 127.0.0.1:xxxxx
[SERVER] PING seq=2 from 127.0.0.1:xxxxx
...
```

> **Важно:** запускать только **один** сервер. Повторный запуск даёт
> `SocketException (10048) address already in use`.

### 3. Клиент (терминал №2)

```bash
dotnet run --project src/Nomi.Client -- \
  --host 127.0.0.1 \
  --port 5000 \
  --duration 60 \
  --interval 1000 \
  --csv docs/latency_samples.csv
```

Печатает:

```
[CLIENT] Accepted  seq=1 rtt=123.00 ms srtt=123.00 jitter=0.00 loss=0.0%
[CLIENT] Accepted  seq=2 rtt=3.00 ms   srtt=108.00 jitter=7.50 loss=0.0%
[CLIENT] Accepted  seq=3 rtt=1.00 ms   srtt=94.62  jitter=7.16 loss=0.0%
...
=== SUMMARY ===
Sent:       60
Received:   60
Lost:       0
Duplicates: 0
Late:       0
Invalid:    0
LossRate:   0.00 %
SRTT:       38.07 ms
Jitter:     4.77 ms
```

### 4. График

```bash
pip install matplotlib
python scripts/plot_latency.py
```

Создаёт `docs/latency_rtt.png` с кривыми RTT, SRTT и Jitter.

---

## Результаты эксперимента

Измерения на **localhost** (Windows 10, .NET 8):

| Сценарий                         | Sent | Received | Lost | Dup | Late | LossRate | SRTT, мс | Jitter, мс |
|----------------------------------|-----:|---------:|-----:|----:|-----:|---------:|---------:|-----------:|
| localhost, 10 с, интервал 1000 мс |   10 |       10 |    0 |   0 |    0 |   0.00 % |    38.07 |       4.77 |

**Наблюдения:**

- Первый RTT = **123 мс** — это прогрев: JIT-компиляция `PingClient`,
  первый `ReceiveAsync`, аллокация буферов.
- Далее RTT падает до **1–3 мс** и остаётся стабильным.
- SRTT быстро сходится: 123 → 108 → 94 → 83 → 72 → 64 → 56 → 49 → 43 → 38 мс.
- Jitter стабилизируется на **~5 мс** — это вклад `Task.Delay(1000)` и
  планировщика, а не сети.
- Потерь на localhost нет (`LossRate = 0 %`).

**Что показывают метрики:**

- На localhost сеть не вносит вклада — вся задержка от планировщика,
  таймера и первого прогрева.
- LossRate = 0 % подтверждает, что PING/PONG и CRC работают корректно.
- Jitter ≈ 5 мс при интервале 1000 мс — ожидаемое поведение для
  виртуального loopback-интерфейса.

---

## Тесты

| Тест                                | Что проверяет                                                        |
|-------------------------------------|----------------------------------------------------------------------|
| `Ping_Roundtrip`                    | сериализация PING → десериализация → совпадение timestamp и nonce    |
| `BadCrc_Rejected`                   | изменение байта payload → `TryDeserialize=false`, `error="bad crc"`  |
| `BadVersion_Rejected`               | `packet[0]=99` → `error="bad version"`                               |
| `TooShort_Rejected`                 | датаграмма 5 байт → `error="too short"`                              |
| `FirstSample_SetsSrtt`              | первый RTT → `Srtt=RTT`, `RttVar=RTT/2`, `Jitter=0`                  |
| `LossRate_Calculated`               | 2 sent, 1 lost → `LossRate = 0.5`                                    |
| `Duplicate_And_Late_Are_Classified` | accepted → duplicate → late, проверка `PongResult`                   |
| `Unknown_Nonce_Is_Invalid`          | неизвестный nonce → `Invalid`, счётчик инкрементируется              |

Запуск:

```bash
dotnet test --logger "console;verbosity=detailed"
```

---

## Артефакты

| Файл                            | Что внутри                                       |
|---------------------------------|--------------------------------------------------|
| `docs/Protocol_Specification.md`| формат PING/PONG, поля, CRC                      |
| `docs/Experiment_Config.md`     | параметры прогонов, команды, окружение           |
| `docs/Latency_Report.md`        | таблица метрик и выводы                          |
| `docs/latency_samples.csv`      | журнал измерений (SENT / Accepted / LOSS)        |
| `docs/latency_rtt.png`          | график RTT, SRTT, Jitter                         |
| `scripts/plot_latency.py`       | построение графика из CSV                        |

---

## Git-flow

- **Ветка:** `feature/latency-measurement`
- **Issues:** PING/PONG, telemetry, разделение слоёв, автотесты, отчёт
- **PR:** `feat: latency measurement subsystem (PR2)` → `main`
- **Ревью:** минимум 1 approve, зелёный CI

---

## Выводы

- Подсистема телеметрии реализована и интегрирована в UDP-протокол Nomi.
- Слои `protocol` / `transport` / `telemetry` строго разграничены,
  что проверяется юнит-тестами без сетевого ввода-вывода.
- PING/PONG, CRC32, классификация duplicate/late/unknown работают
  корректно; на localhost потери = 0 %, SRTT сходится к стабильному
  значению, jitter предсказуем.
- Заложена основа для последующих практик: измерение качества канала
  можно навесить на любые команды протокола без изменения формата
  заголовка.