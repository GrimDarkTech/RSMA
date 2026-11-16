# Сборка и тестирование

Команды ниже предполагают workspace `F:\RSMA-Refactored`, Unity repository
`RSMA` и native firmware repository `rsma-mcu`. Используйте фактические пути
своей рабочей копии.

## Собрать firmware

`rsma-mcu` предоставляет native tools, CTest и optional Clang/libclang source
builder. Если `rsma-build` включён и настроен с libclang:

```powershell
Set-Location F:\RSMA-Refactored\rsma-mcu
cmake --build build --config Debug --parallel 2
ctest --test-dir build -C Debug --output-on-failure
```

Пример source fixture:

```powershell
build\Debug\rsma-build.exe tests\fixtures\i2c_temperature_fan.cpp -o `
  ..\RSMA\Assets\Resources\RSMA\Firmware\i2c_temperature_fan.rsfw.bytes
```

Конкретная конфигурация и target locations зависят от CMake generator. Для
Clang builder требуется build с `RSMA_MCU_BUILD_BUILDER=ON` и libclang
(при нестандартной установке задайте `LLVM_INSTALL_PREFIX`). После сборки
проверьте пакет `rsma-firmware-inspect` и manifest/peripheral requirements до
подключения его к Unity сцене.

Текущая fixture `tests/fixtures/i2c_temperature_fan.cpp` демонстрирует
register-level I2C read, calibration/filter, PWM fan output и UART telemetry.
Она не зависит от Arduino `Wire` facade.

## Подключить firmware в Unity

1. Поместите бинарный пакет в Unity `Assets/Resources`, предпочтительно с
   окончанием `.rsfw.bytes`, чтобы он импортировался как `TextAsset`.
2. Назначьте его полю Firmware компонента `RSMAVirtualMicrocontroller` либо
   используйте согласованный Resources key.
3. Сверьте manifest Board ID с Expected Board ID компонента.
4. Проверьте `VirtualPinBinding` для используемых GPIO: virtual ID, физический
   `RSMAGPIO` pin и допустимый profile route должны совпасть.
5. Зарегистрируйте нужные `VirtualI2cDevice`/`VirtualSpiDevice` и начальные
   registers до firmware read; для аналогового входа зарегистрируйте
   `VirtualAnalogSensor` на допустимом ADC channel.
6. Запускайте и проверяйте `ExecutionState`, `FaultMessage`, trace, bus
   diagnostics и PWM/UART observable outputs.

## Проверки

Основной скрипт `rsma-mcu/tests/run_validation.ps1` собирает native targets,
выполняет CTest и собирает managed runtime, EditMode и PlayMode test assemblies.
С `-SkipUnity` Unity Test Runner не запускается:

```powershell
Set-Location F:\RSMA-Refactored\rsma-mcu
& '.\tests\run_validation.ps1' -SkipUnity
```

Здесь `-SkipUnity` пропускает запуск EditMode и PlayMode в Unity; managed build
test assemblies всё равно проверяется. Для полного EditMode и PlayMode batch run задайте
`UNITY_EXECUTABLE` или передайте `-UnityExecutable`.

Распределение тестов:

- `rsma-mcu/tests/firmware_tests.cpp` — native format, validator, profiles и
  protocol checks;
- `rsma-mcu/tests/rsma_build_tests.cmake` плюс `tests/fixtures/` — positive и
  negative compiler/source fixtures;
- `RSMA/Assets/Tests/EditMode/` — format reader, backend, board profiles,
  scheduling, peripheral state и регрессии;
- `RSMA/Assets/Tests/PlayMode/` — end-to-end VM/scene behavior, bus devices,
  trace и observable outputs.

## Тестовая матрица для изменений

Для нового source/HAL API тестируйте:

- принятие корректного source usage и ожидаемый HAL/bytecode lowering;
- отказ компилятора на неверном числе аргументов, range и неподдерживаемой форме;
- отказ reader/backend на невалидных operands, board ID, routes/capabilities;
- runtime success, invalid order/argument, absent device, NACK и timeout;
- reset во время и после использования периферии;
- trace/diagnostic и safe-state при fault;
- одинаковое поведение одного `.rsfw` в standalone VM и Unity acceptance path,
  когда это входит в затронутый контракт.

Не удаляйте пользовательские или уже созданные output files и не запускайте
Unity batch jobs параллельно на одном project directory. Если Unity Editor не
доступен, сообщайте отдельно, что прошёл managed build, а какие acceptance tests
остались незапущенными.