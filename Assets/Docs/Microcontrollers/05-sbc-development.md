# Следующее направление: одноплатные компьютеры (SBC)

Целевой и единственный транспорт SBC↔RSMA для SITL и HITL в этой задаче — локальная
сеть WiFi/Ethernet. SBC рассматривается как отдельное сетевое устройство со своим
адресом. Этот документ различает реализованный Unity-side
вертикальный срез, host-side проверки и ещё не завершённую проверку LAN-доступа
из VM/физического SBC. Подробный roadmap и выводы исследования uDTP находятся в
[общем SBC плане](../../../../docs/План-разработки-SBC.md),
[фактическом контракте uDTP](../../../../docs/Этап-0-Фактический-контракт-uDTP.md)
[uDTP v1 wire-контракт](../../../../docs/Этап-1-Контракт-uDTP-v1.md).

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
- будущий Linux/HITL provider — отдельная реализация для GPIO SBC,
  например через `libgpiod`, с capability discovery, ownership и safe-state.

Транспорт RSMA/uDTP должен переносить semantic commands/telemetry и capability
discovery. Не следует отправлять каждый быстрый GPIO edge отдельным ZeroMQ
сообщением; для частых событий нужен bounded batch или HITL stream envelope.

## SBC — не большой микроконтроллер

Типичный SBC запускает полноценную Linux OS, процессы/threads, драйверы ядра,
userspace services и сетевой stack. Его нельзя моделировать как ещё один набор
пинов или запускать на нём `.rsfw`. Для этой задачи RSMA взаимодействует с
приложением SBC через LAN; аппаратные периферийные линии являются отдельным
локальным hardware provider boundary.

Разделяйте два сценария:

- **SITL**: Linux-приложение выполняется в Ubuntu VM VirtualBox с адаптером
  Bridged. VM имеет собственный адрес общей локальной сети и подключается к
  LAN-адресу host RSMA через тот же сетевой RSMA/uDTP client API, что и HITL.
- **HITL**: Linux-приложение выполняется на физическом SBC, который пользователь
  вручную запускает и подключает к общей WiFi/Ethernet-сети как отдельное
  устройство. SBC подключается к LAN-адресу host RSMA по тому же client API.

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
        |     LAN address of RSMA host; never localhost for a remote SBC
        |
        +-- optional OS hardware providers
              GPIO / I2C / SPI / PWM / ADC
        |
        +-- health, reconnect, permissions, diagnostics
```

В RSMA Unity-side уже есть NetMQ server path и `DataBroker`, но это не доказывает
завершённую LAN SITL/HITL интеграцию или готовность SBC device management. Путь
должен оставаться layered: transport не должен знать Unity
GameObject, а RSMA scene actuator/sensor не должен напрямую управлять Linux
device files.

## Уже существующий RSMA/uDTP boundary

В текущем `NetMqServer` используются endpoints по умолчанию:

| Порт | RSMA роль | Обычная клиентская роль |
|---:|---|---|
| 5555 | ROUTER для команд | REQ |
| 5556 | PUB для топиков RSMA -> клиент | SUB |
| 5560 | SUB для топиков клиент -> RSMA | PUB |

RSMA сокеты bind-ятся на `tcp://*:<port>`, поэтому удалённый клиент должен
использовать реальный LAN-адрес host, а не `127.0.0.1`. Нужно разрешить входящие
порты 5555, 5556 и 5560 в firewall и проверить доступность listener из VM/сети.
Loopback пригоден только для host-local тестов. Клиентская конфигурация
`SbcDevice` сейчас имеет `tcp://127.0.0.1` по умолчанию — для отдельного SBC/VM
её обязательно задавать LAN-адресом host.

RSMA bind-ит сокеты на `tcp://*:<port>`, то есть принимает соединения на сетевых
интерфейсах host. На клиенте нужно указать LAN-адрес host; `127.0.0.1` подходит
только для host-local проверки. Для отдельной VM/SBC должны быть доступны через
маршрутизацию и firewall порты 5555, 5556 и 5560.

Роли и порты можно настроить при запуске сервера. Команды в исходниках включают
`GetTopics`, `GetServerStatus`, `RestartLevel`, `PrintMessage:<text>`; список
команд и ограничения доступа нужно перепроверять в текущем `NetMqServer.cs` до
изменения публичного API.

Текущий topic transport формирует multipart из трёх кадров: UTF-8 topic name,
20-byte uDTP v1 header и payload. Заголовок кодируется little-endian: `timestamp`
(int64), `sequence` (uint32), `payloadSize` (int32), `topicTypeId` (int32).
Unity использует явные codecs и стабильный registry IDs (типизированные типы
начинаются с `1001`); известные typed входящие сообщения проверяются по размеру,
декодируются до обновления `DataBroker` и затем доступны приложению. Opaque режим
ограничен ID `0` и legacy camera ID `2`; неизвестные типизированные IDs
отклоняются. Не считайте произвольный managed struct layout wire ABI: контракт
задаётся codecs и общими vectors в
[`docs/Этап-1-Контракт-uDTP-v1.md`](../../../../docs/Этап-1-Контракт-uDTP-v1.md).

Не всякий сетевой topic автоматически управляет объектом сцены. Для этого
необходимы typed decoder, validation, явная маршрутизация и handler на Unity main
thread. Текущая Unity сцена демонстрирует такой путь для `RobotVelocity` и
`ArmCommand`.

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
configuration and diagnostics, but управление VM and network-session GUI относятся
к этапам 5–6 и намеренно не входят в этот vertical slice.

Сцена `Assets/Scenes/TestScenes/SBC.unity` содержит импортированную модель
Raspberry Pi 5 (`raspberry-pi-5.1`) с `SbcDevice` и `SbcSceneTelemetry`. Adapter
с заданным интервалом публикует `RobotVelocity` из движения `Transform` и
применяет входящий `ArmCommand` к сценовому состоянию `IsArmed` на Unity main
thread. Это демонстрационный sensor/command path, а не модель Linux GPIO или
управление VM.


`SbcSceneTelemetry.EnsureSubscription()` повторно связывает scene adapter с
`SbcDevice` после создания/перезагрузки компонентов. Это важно для EditMode
инструментов и scene reload, где Unity lifecycle callbacks могут быть вызваны не
в том же порядке, что в обычном Play Mode.
Scene acceptance test дополнительно продвигает simulated time прошивки
`blink.rsfw.bytes` и проверяет переходы `PC.13`/virtual pin `13` и состояния
`Light.enabled`. Это подтверждает путь `.rsfw -> VirtualMcuBackend -> GPIO ->
LED`, а не только наличие ссылок в YAML сцены.

## SITL/HITL baseline

Целевая первая SITL-схема использует VirtualBox через `VBoxManage` и сетевой
адаптер Bridged. Guest profiles — `Ubuntu 22.04` и `Ubuntu 24.04`; целевая VM
называется `RSMA-Ubuntu-SITL`, получает собственный адрес в общей локальной
WiFi/Ethernet-сети и подключается к host RSMA по его LAN-адресу. NAT и
`10.0.2.2` не являются частью целевой схемы. `SbcVirtualBoxController` уже
отделяет запуск процесса от Unity-компонента через
`ISbcVirtualBoxCommandRunner`, проверяет состояние VM и предоставляет явные
`StartAsync`/`StopAsync`. Однако текущая `SbcSitlConfiguration.Validate()`
отклоняет режим Bridged и требует NAT с guest gateway `10.0.2.2`; следовательно,
целевая конфигурация Bridged пока **не реализована**. Unity GUI управления SITL
также остаётся работой следующих этапов. Реальную VM нужно зарегистрировать и
подготовить отдельно; отсутствие `VBoxManage`, VM или доступного сетевого
endpoint должно давать понятную ошибку, а не переводить сцену в ложное Running
состояние.

Управление SITL является частью Unity GUI, а не обязательным внешним
host-side script. По паттерну файловых инструментов RSMA, включая
`RSMA/Assets/Scripts/GUI/MCFirmware.cs`, оператор задаёт имя VM, путь к
`VBoxManage`, host/SBC адреса, порты и таймауты. Пути к необходимым исполняемым
файлам, образам, проектам и каталогам выбираются через установленный
`StandaloneFileBrowser`; GUI обязан валидировать значения до операции Start.
Для удалённого guest client указывается достижимый LAN-адрес host RSMA; loopback
`127.0.0.1` применим только для локальных тестов. Запуск VM не выполняется
автоматически при загрузке сцены.

Ubuntu guest используется как среда разработки: Python uDTP unit tests (`8/8`)
и C++ shared-vector checker проходят; четыре репозитория клонированы в
`~/Projects/RSMA`. Ранее проверенная VM `PX4-Autopilot` по Bridged LAN была
нестабильна и выключалась. Полноценный сквозной запуск клиента отдельной VM
против Unity listener по LAN не подтверждён. Host должен быть доступен из guest
по LAN, а порты listener должны быть разрешены firewall.

Пока VM нестабильна, host-native live smoke-test проверен без перезапуска Unity:
Python и C++ clients обращаются к `GetServerStatus`, получают
`sbc-01/sensor/velocity` с type ID `1001`, публикуют typed сообщения, после чего
Unity регистрирует входящие topics. Полный клиентский CI также проходит:
Python↔standalone-C# и C++↔standalone-C# integration. Это подтверждает host-side
RSMA Unity path, но не VM isolation, Bridged guest-to-host path или подключение
физического SBC через LAN.

API клиента следует строить вокруг capabilities и typed operations, а не
переносить Unity-классы или низкоуровневые OS descriptors через сеть.

### Общий сетевой путь SITL и HITL

HITL использует тот же RSMA/uDTP client path и те же проекты, что и SITL-клиент
в Ubuntu VM. Отличие состоит только в среде выполнения: в HITL это реальный SBC,
который пользователь запускает вручную и подключает к RSMA через общую локальную
WiFi/Ethernet-сеть. RSMA не управляет питанием или запуском физической платы;
Unity GUI отображает выбранный режим, endpoint, health и состояние reconnect.

Для обоих режимов используется только общий сетевой RSMA/uDTP client path.
Подключение к RSMA выполняется по LAN-адресу host; ни для SITL, ни для HITL не
предполагается отдельный проводной transport.

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
6. Выбрать первую SBC/OS и только затем реализовать GPIO/I2C/SPI/PWM/ADC
   providers по реальным capability discovery, permissions и hardware tests.
7. Разделить CI на обычные unit/protocol jobs, Unity integration, optional VM/SITL
   и hardware HITL; hardware отсутствие не должно блокировать protocol CI.

Не объявляйте пункт завершённым только по наличию интерфейса или тестового
mock: нужны shared vectors, interop tests, timeout/disconnect/error cases и
проверка реального RSMA path там, где он входит в критерий.