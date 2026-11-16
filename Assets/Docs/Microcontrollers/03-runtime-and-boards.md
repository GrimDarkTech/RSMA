# Runtime, платы и виртуальная периферия

## RSFW и запуск

RSFW v1 — бинарный контейнер с 64-byte header, 32-byte section entries,
little-endian integer fields, manifest, bytecode и CRC32. Reader проверяет magic,
версии, границы секций, контрольные суммы, opcode/operand sizes и entry point
до исполнения. HAL_CALL v1 имеет 4 operand bytes: `service:u16`, `argument:u8`,
`value:u8`. Точная спецификация: [`FIRMWARE-FORMAT.md`](../../../../rsma-mcu/docs/FIRMWARE-FORMAT.md).

Пакет объявляет board ID, память, entry point и требуемые периферии/routes.
Backend сверяет требования с capabilities. Отсутствующий peripheral или
несовместимая плата должны приводить к preflight error, а не к частичному
запуску.

## Board profile и pin routing

`VirtualBoardProfile` описывает:

- стабильный virtual pin ID и основной physical route `port.pin`;
- digital input/output, pull-up/down, open-drain, ADC, PWM и external IRQ
  capabilities;
- alternate functions с именем и route (например `PWM@TIM...`, `SPI@...`);
- reset mode/pull/output type/latch state и дополнительные допустимые physical
  routes.

Профиль проверяет duplicate virtual pins, повторное использование physical pins,
дубликаты alternate routes и несовместимые reset settings. Backend отдельно
проверяет требуемые firmware pins/routes и наличие scene binding в активном GPIO
provider. Текущий STM32 profile содержит дополнительные routes только для
описанного поднабора pins; AVR profile также является ограниченным виртуальным
профилем. Не считайте virtual pin label автоматически равным GPIO, ADC или bus
route реального чипа. Добавление pin/interface требует подтверждённой разводки.

В текущем Unity коде существуют profile factories `RSMA-STM32F103` и
`RSMA-AVR-ATmega328P`. Их карта намеренно ограничена описанным набором pins и
routes: не предполагайте, что все физические GPIO/alternate functions модели
реального чипа уже представлены.

## GPIO и электрические состояния

Не смешивайте:

- mode и конфигурацию (input/output, pull, push-pull/open-drain);
- output latch (что записала прошивка);
- effective line (результирующий уровень после pull/external drive);
- виртуальный внешний drive и его конфликт с output driver.

GPIO adapter связывает virtual pin с компонентом `RSMAGPIO`, но не добавляет
автоматически все MCU peripherals. В частности, `RSMAGpioHalAdapter` сообщает,
что scene GPIO adapter сам не предоставляет SPI, I2C, DMA и EEPROM.

В `RSMAVirtualMicrocontroller` создаются отдельные `VirtualGpioBank`,
`VirtualUartBank` и `VirtualBusBank`, а затем объединяются backend-ом. Это
разделение намеренно: GPIO mapping не должен становиться register device model.

## UART, buses, ADC, PWM и DMA

| Периферия | Модель и внешняя точка |
|---|---|
| GPIO/IRQ | Board profile + GPIO HAL adapter; timestamped input events, edge queue и scheduling diagnostics. |
| ADC | `analogRead()` выдаёт `0..4095`; `VirtualAnalogSensor` вычисляет физический сигнал по simulated time и регистрируется через analog input contract. |
| PWM | duty `0..255`, default `490 Hz` для двухаргументного source API, optional frequency overload; scene actuator потребляет состояние. |
| UART | 4 source ports, baud presets, bounded 256-byte RX/TX FIFOs, scheduled RX, TX observable event, byte/packet callbacks и diagnostics. |
| I2C | `VirtualBusBank` регистрирует device по 7-bit address; register transaction моделирует ACK/NACK, repeated-start, speed, clock stretch и timeout. Старый byte-level `i2cTransfer` остаётся response stub. |
| SPI | Device регистрируется по `(bus, chip-select)`; register transaction принимает mode `0..3`, bit order и speed/timeout. Старый byte-level `spiTransfer` остаётся response stub; source `spiReadRegister`/`spiWriteRegister` используют ограниченный маршрут с mode 0/MSB-first. |
| DMA | Synchronous transfer и bounded asynchronous queue/buffers/tickets там, где board profile поддерживает DMA. STM32 и AVR profiles могут различаться по capabilities. |
| EEPROM | Persistent byte storage; обычный VM reset не означает очистку хранилища. Очистка — отдельная host/backend операция. |

Register-level bus API не тождественен универсальному Arduino bus API. Текущие
firmware fixture показывают `i2cReadRegister`/`i2cWriteRegister` и
`spiReadRegister`/`spiWriteRegister`; source helpers используют один байт на
операцию. Host-side `VirtualBusBank` способен на более общие transfers, но эти
возможности не все доступны firmware source API. Для более сложного драйвера
сначала определите минимальный transfer contract и расширьте все слои.

## Lifecycle, reset и safe state

- **Configure/preflight** проверяет board ID, required peripherals/routes,
  profile, pin capabilities и scene mappings до выполнения инструкции.
- **Start/Running** допускается только для configured backend.
- **Stop** и **SafeState** должны выключить/перевести output peripherals по их
  политикам.
- **Reset** сбрасывает VM time/stack/global state и вызывает reset каждого
  peripheral lifecycle owner. EEPROM сохраняется по текущему контракту.
- **Fault** фиксирует ошибку, переводит VM/backend в явное fault/safe state;
  NACK/timeout нельзя превращать в успешный нулевой sample.

При добавлении peripheral документируйте, очищаются ли очереди, diagnostics,
pending transactions, registers, calibration и storage для каждого перехода.
Также определите, что происходит при `OnDisable`, повторной инициализации и
остановке Unity Play Mode.

## Время, trace и диагностика

Virtual scheduling использует simulated time. Входные GPIO/UART события имеют
timestamp и устойчивый порядок; host clock policy и maximum delta задаются
компонентом/backend. Не используйте `DateTime` или частоту кадров для логики
firmware.

`VirtualMachine` предоставляет bounded trace buffer (default 4096 events),
monotonic sequence, timestamp, channel/value/message и dropped-event count.
`RSMAVirtualMicrocontroller` отдаёт snapshot и deterministic JSON export.
Bus/UART/DMA/scheduling diagnostics имеют отдельные контракты и события.
Пользуйтесь ими в tests и инструментах диагностики, а не парсите Unity log.

При разработке fault path проверяйте одновременно:

- код/сообщение diagnostic и маршрут устройства;
- VM state и точку, где прошивка остановлена;
- trace до и после ошибки;
- output safe-state;
- поведение после явного reset.