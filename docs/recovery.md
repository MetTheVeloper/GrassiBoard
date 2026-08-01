# Recovery

No kernel driver, Windows service, test certificate, test-signing setting, or audio endpoint is installed by Milestone 2. Recovery consists of stopping/closing the app and deleting its extracted folder. If monitoring remains active after an abnormal exit, close the process in Task Manager; WASAPI streams are released when the process ends.

Full Safe Mode, PnPUtil, Driver Verifier, Windows Audio service, certificate, and test-signing recovery procedures will be added before the first driver test package is distributed.
