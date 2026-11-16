# Архитектура и границы API

В RSMA сосуществуют три понятия, которые нельзя считать взаимозаменяемыми:

1. **Legacy MCU-компоненты Unity** — `RSMAMicrocontroller`, `RSMAGPIO`, `RSMADataTransferMaster` и пользовательская coroutine-программа.
2. **Virtual MCU firmware runtime** — пакет `.rsfw`, C# bytecode VM, board profile и виртуальные периферийные banks.
3. **SBC integration** — будущий Linux-процесс/устройство, общающееся с RSMA по сетевому контракту. Это не MCU-прошивка и не target для `.rsfw`.

## Virtual MCU: путь сигнала

```text
C/C++-подобный firmware source
        |
        v
rsma-build (Clang frontend, поддерживаемый subset)
        |
        v
.rsfw: manifest + bytecode + optional symbols/debug
        |
        v
RsfwReader -> VirtualMachine -> VirtualMcuBackend
                                      |
              +-----------------------+----------------------+
              |                       |                      |
       board profile          UART / bus / DMA         GPIO adapter
              |                       |                      |
              +-----------------------+----------------------+
                                      |
                             RSMA scene components
```

`.rsfw` — переносимый пакет для RSMA VM. Это не ELF, HEX или BIN, не исполняется
физическим STM32/Arduino и не является образом Linux для SBC.

### Владельцы состояния

| Слой | Ответственность |
|---|---|
| `rsma-build` | Разбор ограниченного исходного языка, проверка compile-time аргументов, lowering в bytecode и выпуск пакета. |
| `RsfwReader` | Проверка заголовка/секций/CRC/версий/operand sizes до исполнения. |
| `VirtualMachine` | Bytecode execution, value/call stack, scheduler, simulated time, callbacks, trace и VM state. |
| `VirtualMcuBackend` | Композиция HAL providers, capabilities, preflight, reset/start/stop/safe-state и маршрутизация вызовов. |
| Board profile | Идентификатор платы, virtual-to-physical pin mapping, capabilities, alternate functions и reset defaults. |
| Peripheral bank | Состояние GPIO, UART, I2C/SPI devices, DMA или EEPROM соответствующего виртуального блока. |
| RSMA scene component | Потребление выходного состояния и представление сигнала/устройства в сцене. Компонент сцены не должен дублировать firmware state. |

Ключевые исходники Unity: `Assets/Scripts/Microcontroller/Virtual/VirtualMcuRuntime.cs`,
`VirtualMcuBackend.cs`, `VirtualBoardProfile.cs`, `VirtualBusDevices.cs` и
`Assets/Scripts/Microcontroller/RSMAVirtualMicrocontroller.cs`.

## Два source/runtime API

### Legacy coroutine API

`RSMAMicrocontroller.OnEnable()` находит `IMicrocontollerProgram`, передаёт ей
ссылки `GPIO` и `dataBus`, создаёт `MainProgramm()` и запускает Unity coroutine.
`OnDisable()` останавливает coroutine и выключает GPIO. Это API непосредственно
работает с компонентами Unity, а не с `.rsfw` и bytecode VM.

`RSMADataTransferMaster` — простой обмен строкой с `RSMADataTransferSlave` из
списка scene-компонентов (`SendData`, `ReciveData`, `Request`). Он не реализует
электрический I2C/SPI, адресацию устройств или временные характеристики этих шин.

Сохраняйте этот путь для существующих сцен и программ. Новую поддержку firmware
VM не следует встраивать в legacy master/slave-классы.

### Virtual firmware API

Source declarations находятся в `rsma-mcu/include/rsma_mcu/Arduino.h`; builder
узнаёт builtin только по объявлению из этого header и понижает поддержанные
вызовы в HAL/bytecode. Обычная компиляция произвольной Arduino library не
поддерживается.

Основные внешние точки Unity-компонента `RSMAVirtualMicrocontroller`:

- назначение `TextAsset` с `.rsfw`, GPIO и ожидаемого board ID;
- `Step()`, `AdvanceSimulation(deltaMilliseconds)`, `Reset()`;
- `ExecutionState`, `FaultMessage`, `SimulatedTimeMilliseconds`;
- регистрация `VirtualAnalogSensor` и виртуальных I2C/SPI устройств;
- доступ к UART transmit events, bus/UART/DMA/scheduling diagnostics;
- `GetTraceSnapshot()`, `ExportTraceJson()`, `ClearTrace()`.

При инициализации компонент загружает пакет, проверяет board ID, создаёт profile,
`RSMAGpioHalAdapter`, `VirtualUartBank`, `VirtualBusBank`, `VirtualGpioBank` и
единый `VirtualMcuBackend`, затем выполняет preflight требований firmware до
исполнения. При ошибке конфигурации пакет не должен частично запускаться.

## Где проводить изменения

- Новый C++-вызов: declaration в `Arduino.h`, compile-time checks/lowering в
  `rsma_build.cpp`, HAL service и validation в native/managed format, runtime
  dispatch, board capability check, positive/negative fixtures и документация.
- Новая виртуальная периферия: узкий HAL interface, единственный владелец её
  состояния, forwarding в backend, lifecycle/reset policy, deterministic tests,
  diagnostics и observable trace/API для RSMA.
- Новый тип платы: единые Board ID и routes в native и Unity profile, capabilities,
  reset/safe state, preflight и тесты совпадения профилей. Не расширяйте pin map
  догадками о физической разводке.
- Новый SBC transport: отдельная граница процесса/сети и adapter к RSMA/uDTP;
  не добавляйте Linux GPIO в MCU VM и не используйте Unity coroutine как сетевой
  runtime.

## Основные инварианты

- Одно значение/состояние имеет один authoritative owner; адаптеры маршрутизируют,
  но не становятся второй моделью шины или платы.
- Невалидная конфигурация и отсутствующая capability обнаруживаются до первого
  firmware tick, когда это возможно.
- Виртуальное время и внешние события должны быть детерминированы и не зависеть
  от частоты Unity frames.
- Reset, normal stop, VM fault и safe-state — разные переходы; для каждого
  peripheral явно задаётся очистка/сохранение состояния.
- Публичные контракты, operand layout и HAL service IDs меняются синхронно в
  native builder/validator и C# reader/runtime.