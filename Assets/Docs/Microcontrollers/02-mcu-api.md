# API виртуального MCU

Поддерживаемый язык — небольшой Arduino-подобный source subset, преобразуемый в
RSMA bytecode. Совпадение имён с Arduino не означает полной совместимости с
Arduino Core, STL, vendor HAL или сторонними libraries.

Канонический список ограничений: [`rsma-mcu/docs/SOURCE-LANGUAGE.md`](../../../../rsma-mcu/docs/SOURCE-LANGUAGE.md)
и [`HAL-API.md`](../../../../rsma-mcu/docs/HAL-API.md).

## Точка входа и выполнение

```cpp
#include <rsma_mcu/Arduino.h>

int main()
{
    pinMode(13, OUTPUT);
    while (true) {
        digitalWrite(13, HIGH);
        delay(250);
        digitalWrite(13, LOW);
        delay(250);
    }
}
```

Поддерживается одна `int main()` без параметров; автоматического `setup()`/
`loop()` нет. Возврат из `main()` завершает VM, return value не является exit
status. `delay(constantMilliseconds)` передаёт управление scheduler до
simulated-time deadline; `yield()` отдаёт управление следующему host tick без
прибавления времени. Polling-циклы обязаны задерживаться или yield-иться.

Frontend поддерживает ограниченное подмножество `int`/`bool`, функций и
аргументов, присваиваний, целочисленной арифметики, сравнений, `if/else`,
поддержанных `while`/`for`, статических `int`/`bool` globals и встроенных HAL
вызовов. Произвольные указатели, массивы, heap/dynamic allocation, templates,
function pointers общего назначения, пользовательские классы и произвольные
Arduino libraries не являются поддержанным контрактом. Точный список syntax и
ошибок поддерживайте в canonical source-language document и fixtures.

## Сводка builtin API

| API | Текущее назначение и ограничения |
|---|---|
| `pinMode(pin, INPUT/OUTPUT)` | GPIO direction; pin/mode должны удовлетворять compile-time и board profile checks. |
| `digitalWrite(pin, LOW/HIGH)` / `digitalRead(pin)` | Digital output/input. Неподдерживаемый pin/mode не должен тихо превращаться в успешный вызов. |
| `analogRead(channel)` | Возвращает raw ADC sample `0..4095`; сенсор физической величины отдельно моделируется через Unity virtual sensor. |
| `analogWrite(pin, duty)` | PWM duty `0..255`; legacy overload использует default frequency `490 Hz`. Pin должен иметь PWM capability/route. |
| `analogWrite(pin, duty, frequencyHz)` | Duty `0..255`, compile-time frequency `1..1,000,000 Hz`; частота и pin проверяются до запуска. |
| `attachInterrupt(pin, handler, CHANGE/RISING/FALLING)` | Только именованный source `void handler()`; deferred, non-reentrant dispatch по GPIO edges. Общие function pointers не поддерживаются. |
| `detachInterrupt(pin)` | Удаляет handler указанного pin. |
| `millis()` / `micros()` | Читают deterministic simulated clock, возвращают младшие 32 бита в signed VM `int`; вызов сам не продвигает время. Для интервалов учитывайте wrap-around. |
| `delay(milliseconds)` / `yield()` | Scheduler operations, а не wall-clock sleep. Используйте их в periodic loops. |
| `Serial`, `Serial1`, `Serial2`, `Serial3` | Фиксированные UART ports `0..3`; `begin(preset)` принимает preset `0..4`, затем доступны polling `available/read/write`. |
| `Serial*.attachReceive(handler)` / `detachReceive()` | Ограниченный byte-RX callback с именованным `void()` handler; не эквивалентен произвольному callback API. |
| `Serial*.attachPacketReceive(...)` и packet accessors | Ограниченная интеграция с валидными HITL frames; это не общий packet/socket API. См. HAL-API. |
| `spiTransfer(bus, value)` / `i2cTransfer(address, value)` | Legacy byte-level response stub; неизвестный route/address может вернуть `0`, поэтому это не проверка присутствия устройства и не полный Arduino `SPI`/`Wire` transaction facade. |
| `spiReadRegister(bus, chipSelect, register)` / `spiWriteRegister(...)` | Register-level SPI helper с явными bus/CS; текущий operand кодирует route в ограниченном диапазоне. |
| `i2cReadRegister(address, register)` / `i2cWriteRegister(...)` | Register-level I2C helper с 7-bit address. Это реализованный путь для простых регистровых драйверов. |
| `dmaTransfer`, `dmaBufferWrite/Read`, `dmaSubmit/status/readByte/release` | Sync/async DMA subset, где DMA capability доступна; async contract имеет bounded buffers/queue и явные statuses/errors. |
| `eepromRead(address)` / `eepromWrite(address, value)` | Byte-addressed backend storage; обычный VM reset не обязан очищать persistent contents. |

### Важно про Wire и SPI

Полный Arduino-style `Wire`/`SPI` facade (`beginTransmission`, repeated-start,
`requestFrom`, `beginTransaction`, transfer ownership и т. п.) не следует считать
реализованным только потому, что есть register helpers и `VirtualBusBank` умеет
I2C/SPI transactions. Совместимость с ограниченным facade является следующим
отдельным этапом `rsma-mcu`; до его реализации простые register-based драйверы
используют существующие helpers. Актуальный статус сверяйте с
[`rsma-mcu/docs/DEVELOPMENT-PLAN.md`](../../../../rsma-mcu/docs/DEVELOPMENT-PLAN.md).

## HAL/runtime semantics, важные для драйверов

- `Serial.begin` принимает preset, а не произвольную скорость в baud. Presets:
  `0=9600`, `1=19200`, `2=38400`, `3=57600`, `4=115200`.
- UART RX/TX FIFO имеют ограниченную ёмкость 256 bytes; overflow и пустой read
  диагностируются. Не трактуйте пустое чтение как валидный `0x00`.
- `analogRead` возвращает raw 12-bit значение. Physical units, calibration,
  waveform/noise/sampling относятся к host sensor model, если явно не включены в
  firmware.
- `spiTransfer`/`i2cTransfer` — совместимые byte-level response stubs. Отсутствующий
  ответ может выглядеть как `0`; используйте register-level helpers для
  транзакций с device presence, ACK/NACK, timeout и диагностируемым VM fault.
- PWM в VM моделируется как duty/frequency state по simulated time, а не как
  гарантия высокочастотного физического toggle на каждом Unity frame.
- I2C/SPI register operations используют отдельный register-bus contract:
  зарегистрированный device, NACK и timeout имеют transaction result и при
  source HAL вызове становятся VM fault, а не успешным нулевым sample.
- На STM32 profile SPI/I2C bus route — отдельная capability от GPIO `PinMode`.
  Не выводите доступность bus transaction только из существования GPIO pin map.
- DMA invalid calls, queue full, premature read и invalid offsets имеют
  диагностируемую ошибочную семантику. Проверяйте статус до использования
  результата и освобождайте завершённые tickets.

## Как добавлять API без расхождения слоёв

Для каждого нового source вызова требуется полный вертикальный срез:

1. Определить exact signature, compile-time/runtime аргументы, диапазоны,
   status/error contract и требуемые board capabilities.
2. Объявить builtin в `Arduino.h`; добавить только явные overloads, которые
   builder действительно различает.
3. Добавить lowering и диагностические ошибки в `rsma_build.cpp`.
4. Добавить/обновить HAL ID и operand contract, сохраняя совместимость RSFW либо
   явно повышая версию.
5. Валидировать размер и допустимые значения operands в native validator и
   `RsfwReader`.
6. Реализовать runtime dispatch над existing backend/peripheral state, а не
   создавать параллельную модель устройства.
7. Добавить positive и negative source fixtures, runtime tests, reset/fault cases,
   затем обновить API и development docs.

Каждый HAL вызов текущего RSFW v1 имеет четыре operand bytes:
`service:uint16 little-endian`, `argument:uint8`, `value:uint8`. Если операции
нужны дополнительные runtime-аргументы, их stack order и validation должны быть
документированы и проверены во всех реализациях.