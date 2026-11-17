# Следующее направление: одноплатные компьютеры (SBC)

Этот документ задаёт инженерные границы следующего этапа, а не объявляет
готовыми Linux GPIO API, SBC runtime, C# client или сетевые capabilities, которых
нет в текущем RSMA MCU runtime. Подробный roadmap и выводы исследования uDTP
находятся в [общем SBC плане](../../../../docs/План-разработки-SBC.md),
[фактическом контракте uDTP](../../../../docs/Этап-0-Фактический-контракт-uDTP.md)
и [uDTP v1 proposal](../../../../docs/Этап-1-Контракт-uDTP-v1.md).

## SBC GPIO boundary

SBC в RSMA рассматривается как контроллер внешней электрической шины. Он не
исполняет MCU firmware и не дублирует `VirtualMcuBackend`. Компонент
`SbcGpioPort` маппит логические SBC GPIO на подключённые virtual MCU pins,
передаёт external drive в authoritative electrical model и читает effective
line/electrical diagnostics обратно. Поэтому к одной линии могут подключаться
виртуальный MCU, датчик или actuator без второй независимой копии pin state.

Текущий provider boundary:

- `ISbcGpioBackend` — общий контракт линии;
- `VirtualMcuSbcGpioBackend` — SITL provider поверх `RSMAVirtualMicrocontroller`;
- будущий Linux/HITL provider — отдельная реализация для Raspberry Pi GPIO,
  например через `libgpiod`, с capability discovery, ownership и safe-state.

Транспорт RSMA/uDTP должен переносить semantic commands/telemetry и capability
discovery. Не следует отправлять каждый быстрый GPIO edge отдельным ZeroMQ
сообщением; для частых событий нужен bounded batch или HITL stream envelope.

## SBC — не большой микроконтроллер

Типичный SBC запускает полноценную Linux OS, процессы/threads, драйверы ядра,
userspace services и сетевой stack. Его нельзя моделировать как ещё один набор
пинов или запускать на нём `.rsfw`.

Разделяйте два сценария:

- **SITL/Software integration**: Linux process, контейнер или VM взаимодействует
  с RSMA через сетевой API; логика устройства работает как process/service.
- **HITL**: физический SBC подключён к внешним GPIO/peripheral линиям и сети;
  аппаратный adapter связывает реальные сигналы с RSMA transport.

Если необходимо точное выполнение MCU firmware на SBC-проекте, это отдельный
MCU/co-processor target с отдельной VM/прошивкой, а не SBC API.

## Рекомендуемые слои будущего решения

```text
Linux SBC application / device service
        |
        +-- typed high-level RSMA client API
        |     topics, commands, session, timeouts
        |
        +-- transport adapter (uDTP over existing ZeroMQ endpoints)
        |
        +-- optional OS hardware providers
              GPIO / I2C / SPI / UART / PWM / ADC
        |
        +-- health, reconnect, permissions, diagnostics
```

В RSMA Unity-side уже есть NetMQ server path и `DataBroker`, но это не доказывает
production readiness межъязыкового протокола или готовность SBC device
management. Путь должен оставаться layered: transport не должен знать Unity
GameObject, а RSMA scene actuator/sensor не должен напрямую управлять Linux
device files.

## Уже существующий RSMA/uDTP boundary

В текущем `NetMqServer` используются endpoints по умолчанию:

| Порт | RSMA роль | Обычная клиентская роль |
|---:|---|---|
| 5555 | ROUTER для команд | REQ |
| 5556 | PUB для топиков RSMA -> клиент | SUB |
| 5560 | SUB для топиков клиент -> RSMA | PUB |

Роли и порты можно настроить при запуске сервера. Команды в исходниках включают
`GetTopics`, `GetServerStatus`, `RestartLevel`, `PrintMessage:<text>`; список
команд и ограничения доступа нужно перепроверять в текущем `NetMqServer.cs` до
изменения публичного API.

Текущий topic transport формирует multipart из трёх кадров: UTF-8 topic name,
20-byte header и payload. `TopicHeader.EncodeV1/DecodeV1` использует
little-endian поля:
`timestamp:int64`, `sequence:uint32`, `payloadSize:int32`, `topicTypeId:int32`.
Стабильные uDTP v1 codecs и shared vectors реализованы только для начального
набора типов. Сам Unity server пока сохраняет legacy ограничения: типизированная
публикация использует runtime hash для большинства type IDs и копирует payload
из managed layout; входной путь принимает не менее трёх кадров и сохраняет raw
payload без полной проверки v1 header/type/size. Совпадение C#/C++ codec-ов для
начальных типов не означает завершённую Python↔Unity интеграцию или миграцию
всех publisher-ов. Не используйте managed struct layout как стабильный wire ABI.

В Unity `DataBroker` хранит typed latest values для локальных публикаций и raw
network buffers/headers/type IDs для входящих сообщений. Входящий topic не
валидируется полностью и не применяется автоматически к объекту симуляции:
для каждого типа нужны decoder, validation, routing и handler на Unity main
thread.

## Требуемый SBC developer API (предложение)

### Реализованный Unity-side vertical slice

`RSMA.SBC.SbcDevice` является scene-компонентом цифрового двойника для первого
сквозного сценария. Он хранит `nodeId`, режим `Sitl`/`Hitl`, advertised TCP
endpoint, три порта существующего `NetMqServer`, request timeout и имена
velocity/arm topics. Компонент не создаёт отдельный ZeroMQ transport: он
подключается к общей Unity-side сессии `NetMqServer` и останавливает её только
если сам был владельцем запуска.

Публичные операции первого инкремента:

- `StartSession` / `StopSession` и `ValidateConfiguration`;
- `PublishVelocity(RobotVelocity)` для sensor-to-RSMA пути;
- `ProcessPendingCommands` для main-thread dispatch входящего `ArmCommand`;
- `State`, `LastError`, `LastArmCommand`, `StateChanged` и `ArmCommandReceived`
  для диагностики и прикладного контроллера.

Командный handler вызывается только из Unity `Update`/`ProcessPendingCommands`;
NetMQ worker не меняет GameObject. SITL/HITL mode пока является явной частью
конфигурации и диагностики, но управление VM и Serial-Bridge относятся к этапам
5–6 и намеренно не входят в этот vertical slice.

Сцена `Assets/Scenes/TestScenes/SBC.unity` содержит импортированную модель
Raspberry Pi 5 (`raspberry-pi-5.1`) с `SbcDevice` и `SbcSceneTelemetry`. Adapter
с заданным интервалом публикует `RobotVelocity` из движения `Transform` и
применяет входящий `ArmCommand` к сценовому состоянию `IsArmed` на Unity main
thread. Это демонстрационный sensor/command path, а не модель Linux GPIO или


`SbcSceneTelemetry.EnsureSubscription()` повторно связывает scene adapter с
`SbcDevice` после создания/перезагрузки компонентов. Это важно для EditMode
инструментов и scene reload, где Unity lifecycle callbacks могут быть вызваны не
в том же порядке, что в обычном Play Mode.
Scene acceptance test дополнительно продвигает simulated time прошивки
`blink.rsfw.bytes` и проверяет переходы `PC.13`/virtual pin `13` и состояния
`Light.enabled`. Это подтверждает путь `.rsfw -> VirtualMcuBackend -> GPIO ->
LED`, а не только наличие ссылок в YAML сцены.
управление VM.

## SITL baseline

Первый SITL backend использует VirtualBox через `VBoxManage`. Поддерживаются
guest profiles `Ubuntu 22.04` и `Ubuntu 24.04`; текущая сцена по умолчанию
настроена на зарегистрированную VM `RSMA-Ubuntu-SITL`, NAT и guest-to-host
address `10.0.2.2`. `SbcVirtualBoxController` отделяет process execution от
Unity-компонента через `ISbcVirtualBoxCommandRunner`, валидирует VM state и
поддерживает явные `StartAsync`/`StopAsync`. Реальную VM нужно зарегистрировать
и подготовить отдельно; отсутствие `VBoxManage` или VM должно давать понятную
ошибку, а не переводить сцену в ложное Running состояние.

API клиента следует строить вокруг capabilities и typed operations, а не
переносить Unity-классы или низкоуровневые OS descriptors через сеть.

Минимальная поверхность:

- создать client с endpoint config, connect/request timeout и identity;
- открыть/закрыть session, heartbeat и получить connection/health state;
- запросить server status и согласованный topic/capability registry;
- publish/subscribe типизированные messages с явной версией и timestamp;
- publish raw bounded payload для camera/point-cloud data по отдельному
  streaming policy;
- подписать конкретный handler, безопасно отменить subscription и сообщать
  decoder/transport errors;
- graceful stop, reconnect/backoff и cancellation;
- read-only operational role отдельно от operator commands.

Это не требование к точным именам методов. Сначала фиксируются wire/schema и
поведение; затем имена должны быть идиоматичны отдельно для Python, C++ и C#.
Синхронный request/reply API и asynchronous topic streaming должны быть явными
разными операциями.

## Device interfaces: hardware provider отдельно от RSMA API

Если SBC должен непосредственно управлять hardware, используйте отдельные OS
providers с capability discovery и injected interfaces:

| Интерфейс | Что зафиксировать до реализации |
|---|---|
| GPIO | Chip/line identity, direction, bias, active level, edge events, line ownership, contention и cleanup. Linux GPIO line names не являются устойчивым переносимым pin numbering. |
| I2C | Adapter index, 7-bit address, register/combined transfer semantics, repeated-start support, per-operation timeout и errno/NACK mapping. |
| SPI | Controller/device route, chip-select ownership, mode `0..3`, bit order, max clock, transfer framing и full-duplex result. Не имитировать arbitrary GPIO CS, если backend этого не гарантирует. |
| UART | Device path/identity, baud/data/parity/stop bits, flow control, framing, buffering и disconnect semantics. |
| PWM | Chip/channel route, frequency/duty range, polarity, enabled/disabled state и cleanup. |
| ADC | Channel/scale/reference, sample timing, raw units, calibration, missing-device errors. Не обещать ADC на плате без аппаратного канала/внешнего adapter. |

Transport-соединение RSMA и локальный hardware interface — разные control/data
planes. По умолчанию сеть переносит semantic telemetry/commands; сырые pin
transitions и частые GPIO edges не следует отправлять через Unity frame loop.
Если нужен remote hardware control, определите отдельные разрешения, lease,
timeouts, rate limits и safe-state, прежде чем включать команды.

## Надёжность и безопасность

- Каждая входная команда проходит schema/version/range validation и лимит размера.
- Время, sequence и session/device identity имеют точные единицы и scope.
- Bounded queues, backpressure/drop policy и counters обязательны для streaming.
- Reconnect не должен повторно применять non-idempotent commands без deduplication.
- На disconnect определите stale-data policy и безопасное состояние физических
  outputs; timeout не равен успешной операции с нулевым значением.
- Командные сокеты не должны выставляться на недоверенную сеть без
  authentication/authorization и явной конфигурации bind address.
- Unity API/scene mutation диспетчеризуется в Unity main thread; NetMQ callbacks
  или transport workers не меняют GameObject напрямую.
- Logs/diagnostics включают session, topic/route, sequence, время, error class и
  lost/dropped counters, но не секреты или неограниченный payload.

## Рекомендуемый порядок реализации SBC

1. Зафиксировать uDTP schema, stable type IDs, byte order, size validation,
   timestamp/sequence semantics и shared cross-language vectors.
2. Проверить request/reply framing, multipart handling, subscriptions, lifecycle
   и thread ownership в существующем RSMA NetMQ server.
3. Реализовать минимальный Python client/server E2E по согласованному протоколу;
   затем добавить C++ и C# clients по тем же vectors.
4. Создать автономный C# conformance server для обычного CI; отдельно проверить
   интеграцию с Unity NetMQ server.
5. Добавить typed sensor-to-RSMA и command-to-device сценарий с explicit Unity
   dispatch, bounds, reconnect и diagnostics.
6. Выбрать первую SBC/OS и только затем реализовать GPIO/I2C/SPI/UART/PWM/ADC
   providers по реальным capability discovery, permissions и hardware tests.
7. Разделить CI на обычные unit/protocol jobs, Unity integration, optional VM/SITL
   и hardware HITL; hardware отсутствие не должно блокировать protocol CI.

Не объявляйте пункт завершённым только по наличию интерфейса или тестового
mock: нужны shared vectors, interop tests, timeout/disconnect/error cases и
проверка реального RSMA path там, где он входит в критерий.