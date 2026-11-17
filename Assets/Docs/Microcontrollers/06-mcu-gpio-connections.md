# Связи MCU, GPIO и SBC

Этот документ описывает, как в RSMA соединяются микроконтроллеры, GPIO,
датчики, светодиоды, моторные драйверы и SBC. В проекте сосуществуют legacy
MCU API, новый virtual MCU runtime и SBC GPIO boundary. Они похожи по названиям,
но имеют разные владельцы состояния и правила подключения.

## Короткая схема

```text
MCU firmware / .rsfw
        |
        v
MCU GPIO backend
        |
        v
Authoritative electrical line
        |
        +-- LED / motor driver / sensor / external device
        |
        +-- another MCU input (через отдельный line adapter)
        |
        +-- SBC GPIO boundary
              |
              +-- SITL: virtual provider
              +-- HITL: future Linux GPIO provider
```

Главный принцип: состояние электрической линии должно иметь одного владельца.
`ConnectedPin` является описанием маршрута, а не отдельным хранилищем сигнала.
Адаптеры передают состояние между владельцами, но не создают вторую копию
шины.

## Три модели MCU

### Legacy MCU

Legacy-компонент наследуется от `RSMAMicrocontroller` и обычно расположен на
одном GameObject с `RSMAGPIO`, `RSMADataTransferMaster` и компонентом,
реализующим `IMicrocontollerProgram`.

При `OnEnable()` `RSMAMicrocontroller` находит программу на том же объекте,
передаёт ей ссылки `GPIO` и `dataBus`, затем запускает `MainProgramm()` как
Unity coroutine. Пример программы:

```csharp
public class Blink : MonoBehaviour, IMicrocontollerProgram
{
    public RSMAGPIO GPIO { get; set; }
    public RSMADataTransferMaster dataBus { get; set; }

    public IEnumerator MainProgramm()
    {
        while (true)
        {
            GPIO.WritePin("PC", "13", RSMAGPIO.High);
            yield return new WaitForSeconds(0.1f);
            GPIO.WritePin("PC", "13", RSMAGPIO.Low);
            yield return new WaitForSeconds(0.1f);
        }
    }
}
```

Внешнее устройство получает `ConnectedPin` и ссылку `RSMAGPIO`, затем читает
или изменяет тот же `GPIOPin` через `GetPin(connectedPin)` или `WritePin`.
Так подключаются старые `RSMALED`, motor drivers и другие компоненты.

Ограничение legacy-модели: `RSMAGPIO` не является полноценной электрической
шиной с несколькими драйверами. Прямое соединение двух MCU через один GPIO
объект не даёт достоверной модели ownership, high impedance, конфликтов
push-pull драйверов и edge scheduling.

### Virtual MCU и `.rsfw`

Новый путь использует `RSMAVirtualMicrocontroller`:

```text
.rsfw TextAsset
   -> RsfwReader
   -> VirtualMachine
   -> VirtualMcuBackend
   -> RSMAGpioHalAdapter
   -> RSMAGPIO / Virtual GPIO state
```

На компоненте задаются Firmware (`.rsfw.bytes` как `TextAsset`), GPIO,
Expected Board ID и список `VirtualPinBinding`. Binding связывает virtual pin
firmware с физическим маршрутом Unity:

```yaml
virtualPin: 13
gpioPin:
  portName: PC
  pinName: 13
```

Board profile дополнительно проверяет capabilities, reset mode, pull mode,
output type и допустимый physical route. Firmware не должна запускаться до
успешного preflight.

Состояние линии в virtual runtime разделено на pin mode, pull mode, output type,
output latch, effective line level, external drive и conflict diagnostic.

### SBC GPIO boundary

`SbcGpioPort` является SBC-side provider boundary. Он отображает logical SBC
pin на virtual MCU pin:

```text
SBC logical pin 17 -> virtual MCU pin 13
```

Публичные операции:

```csharp
sbcGpio.Write(17, true);
bool level = sbcGpio.Read(17);
sbcGpio.Release(17);
```

`SbcGpioPort` использует `ISbcGpioBackend`. Сейчас реализован
`VirtualMcuSbcGpioBackend`, который вызывает `RSMAVirtualMicrocontroller`:

- `SetExternalDrive` — внешний drive со стороны SBC;
- `ClearExternalDrive` — перевод SBC линии в high impedance;
- `GetElectricalState` — чтение effective line и conflict diagnostics.

В будущем тот же интерфейс может иметь Linux/HITL реализацию через `libgpiod`
или другой согласованный provider.

## Как подключить LED к virtual MCU

1. Создайте GameObject с `RSMAGPIO`.
2. Добавьте порт `PC` и pin `13`.
3. Добавьте `RSMAVirtualMicrocontroller`.
4. Назначьте firmware `.rsfw.bytes`.
5. Назначьте его `gpio` на созданный `RSMAGPIO`.
6. Добавьте binding `virtualPin 13 -> PC.13`.
7. На LED задайте `virtualMicrocontroller` — тот же MCU и `virtualPin` — `13`.
8. Для legacy fallback задайте `connectMicrocontroller` и `connectedPin = PC.13`.

В сцене `Assets/Scenes/TestScenes/SBC.unity` этот wiring подготовлен:

```text
raspberry-pi-5.1
  +-- SbcDevice
  +-- SbcSceneTelemetry
  +-- SbcGpioPort: logical 17 -> MCU 13

Virtual MCU object
  +-- RSMAGPIO: PC.13
  +-- RSMAVirtualMicrocontroller: virtual 13 -> PC.13
  +-- blink.rsfw.bytes

LED
  +-- virtualMicrocontroller -> same MCU
  +-- virtualPin -> 13
  +-- legacy connectedPin -> PC.13
```

`blink.rsfw.bytes` переключает pin 13. Если firmware не назначена или не
совпадает с Board ID, MCU должен остаться в fault/preflight state.

## Как подключать датчик или actuator

Для устройства, которое только потребляет линию, используйте тот же GPIO route
и читайте effective value через authoritative provider. Для виртуального
датчика входной сигнал должен поступать через external drive или scheduled input:

```text
sensor model -> external drive -> electrical line -> MCU digitalRead / SBC Read
```

Для actuator:

```text
MCU output latch -> electrical line -> ConnectedPin consumer -> device model
```

Не изменяйте `GPIOPin.value` напрямую из нового устройства, если оно должно
моделировать внешний drive. Используйте backend/adaptor API, иначе невозможно
корректно диагностировать конфликт драйверов.

## Два MCU и внешняя линия

Непосредственное подключение двух legacy MCU к одному `RSMAGPIO` не является
поддержанным способом моделирования двух драйверов. Нужен отдельный line adapter,
который имеет endpoint для каждого MCU, вычисляет effective line с учётом
input/high-impedance/open-drain, сообщает line level обоим входам и выдаёт
conflict diagnostic при несовместимых push-pull drives.

`SbcGpioPort` уже следует этому направлению через backend abstraction, но
универсальный `McuGpioLine` adapter для MCU↔MCU пока не добавлен.

## MCU и SBC в одной сцене

1. Создайте virtual MCU и настройте его firmware/board profile.
2. Убедитесь, что MCU pin binding проходит preflight.
3. Создайте `SbcGpioPort` на объекте SBC.
4. Назначьте ссылку на `RSMAVirtualMicrocontroller`.
5. Добавьте logical-to-virtual pin bindings.
6. Подключите LED, sensor или actuator к тому же MCU pin/electrical route.
7. Проверьте `VirtualGpioElectricalState` и trace при запуске.
8. Только после этого подключайте SBC client/API или SITL guest.

## Ограничения

- полноценный Linux GPIO backend для физического Raspberry Pi ещё не реализован;
- универсальное соединение двух MCU через одну электрическую линию ещё не реализовано;
- полная Arduino `Wire`/`SPI` compatibility facade не является текущим контрактом;
- физический Serial-Bridge и длительный HITL прогон относятся к отдельным этапам.