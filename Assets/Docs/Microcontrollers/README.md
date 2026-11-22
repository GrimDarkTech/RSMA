# Микроконтроллеры и платы в RSMA

Документация для разработчиков, которые меняют MCU runtime, периферию Unity,
формат прошивки или готовят поддержку одноплатных компьютеров (SBC).

## С чего начать

- [Архитектура и границы API](01-architecture.md) — какие в RSMA есть модели MCU и как они связаны с периферией сцены.
- [API виртуального MCU](02-mcu-api.md) — исходный C++ subset, HAL, GPIO, ADC/PWM, UART, шины и ограничения.
- [Платы, runtime и диагностика](03-runtime-and-boards.md) — RSFW, board profiles, жизненный цикл, trace и fault handling.
- [Сборка и проверка](04-build-and-test.md) — как собрать firmware и запускать native, .NET и Unity тесты.
- [SBC: целевая архитектура](05-sbc-development.md) — границы, API, протоколы и план следующего направления.
- В `05-sbc-development.md` также зафиксированы текущие окна `RSMA/SBC VirtualBox Settings` и `RSMA/SBC SITL`, VM window mode, auto-start/stop, управление логами и статус проверок NetMQ lifecycle.
- [Связи MCU, GPIO и SBC](06-mcu-gpio-connections.md) — практическая настройка legacy MCU, virtual MCU, LED, датчиков и SBC GPIO boundary.

## Источники истины

Подробные контракты и реализации распределены между несколькими репозиториями:

- `RSMA/Assets/Scripts/Microcontroller/Virtual/` — Unity C# runtime, board profiles, backend и модели шин.
- `rsma-mcu/include/rsma_mcu/Arduino.h` и `rsma-mcu/tools/rsma_build.cpp` — builtin source API и его lowering.
- `rsma-mcu/docs/HAL-API.md`, `SOURCE-LANGUAGE.md`, `FIRMWARE-FORMAT.md` и `DEVELOPMENT-PLAN.md` — контракты `.rsfw` и языка прошивок.
- `docs/План-разработки-SBC.md`, `Этап-0-Фактический-контракт-uDTP.md` и `Этап-1-Контракт-uDTP-v1.md` в корне workspace — SBC/uDTP план и зафиксированные результаты исследования.

Эта папка даёт навигацию и описывает взаимодействие слоёв. При расхождении деталей проверяйте перечисленные исходники и канонические документы соответствующего репозитория. Предложения для будущего SBC API помечены как проектные, а не как уже существующие публичные интерфейсы.