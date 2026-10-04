# Конфигурация эксперимента latency

## Параметры
- Сервер: 127.0.0.1:5000
- Интервал PING: 1000 мс
- Таймаут PONG: 500 мс
- Длительность: 60 с
- SRTT alpha = 1/8, RTTVAR beta = 1/4, Jitter alpha = 1/16

## Сценарии
1. localhost
2. Wi-Fi LAN

## Команды
```bash
dotnet run --project src/Nomi.Server -- --port 5000
dotnet run --project src/Nomi.Client -- --host 127.0.0.1 --port 5000 --duration 60