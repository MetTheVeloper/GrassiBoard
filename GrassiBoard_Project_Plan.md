# پروژه GrassiBoard

## 1. هدف پروژه

یک اپلیکیشن اختصاصی Windows x64 با نام **GrassiBoard** بساز که امکانات زیر را فراهم کند:

1. دریافت زندهٔ صدای میکروفون یک هدست USB
2. تغییر Real-time صدای میکروفون با کمترین تأخیر ممکن
3. تغییر Pitch بدون تغییر سرعت صحبت
4. کنترل یا حفظ Formant برای طبیعی‌تر شدن صدا
5. ترکیب صدای پردازش‌شدهٔ میکروفون با Soundboard
6. مانیتورینگ اختیاری صدا از طریق همان هدست USB
7. ارسال خروجی نهایی به یک Virtual Microphone اختصاصی
8. قابل انتخاب بودن Virtual Microphone در Discord، OBS، مرورگر و سایر برنامه‌ها
9. Build خودکار اپ، موتور صوتی و درایور با GitHub Actions
10. ارائهٔ نسخهٔ قابل دانلود بعد از هر Push
11. پیشروی مرحله‌به‌مرحله و توقف پس از هر مرحله تا زمانی که کاربر نسخه را آزمایش و تأیید کند

این پروژه فقط برای استفادهٔ شخصی ساخته می‌شود و قرار نیست عمومی یا تجاری منتشر شود.

---

# 2. قوانین اصلی همکاری

## 2.1 پیشروی مرحله‌ای

فقط یک Milestone را در هر مرحله انجام بده.

پس از پایان هر Milestone:

1. تمام تغییرات را Commit کن.
2. GitHub Actions باید با موفقیت اجرا شود.
3. خروجی قابل نصب یا اجرا باید به‌عنوان Artifact تولید شود.
4. فایل `CHANGELOG.md` را به‌روز کن.
5. فایل `docs/current-status.md` را به‌روز کن.
6. شمارهٔ نسخه را افزایش بده.
7. دستور تست دقیق و کوتاه به زبان فارسی ارائه کن.
8. نتیجه‌ای را که واقعاً آزمایش نشده، «تست‌شده» اعلام نکن.
9. منتظر نتیجهٔ تست کاربر بمان.
10. تا دریافت تأیید صریح کاربر وارد Milestone بعدی نشو.

اگر تست کاربر شکست خورد، فقط همان Milestone را اصلاح کن و Patch Version جدید بساز.

مثال:

```text
v0.3.0  نسخهٔ اولیهٔ Milestone
v0.3.1  اصلاح اولین مشکل گزارش‌شده
v0.3.2  اصلاح دوم
v0.4.0  شروع Milestone بعدی پس از تأیید
```

## 2.2 حفظ Build سالم

هیچ Commitی نباید عمداً Build اصلی را خراب کند.

در صورت نیاز به کار آزمایشی:

- از Branch جداگانه استفاده کن.
- بعد از موفقیت Build آن را Merge کن.
- فایل‌های موقت، Binaryهای محلی و کلیدهای خصوصی را Commit نکن.

## 2.3 صداقت در تست

می‌توان موارد زیر را در GitHub Actions تست کرد:

- Build اپ
- Build موتور C++
- Build درایور
- Unit Testهای DSP
- تست فایل صوتی
- بررسی Driver Package
- بررسی Signature
- Packaging

اما نصب واقعی درایور، دریافت صدا از هدست USB و تست در Discord باید توسط کاربر روی کامپیوتر واقعی انجام شود، مگر اینکه یک Self-hosted Runner واقعی برای این کار در اختیار پروژه قرار گرفته باشد.

---

# 3. معماری اجباری

## 3.1 پردازش صوتی در User Mode

تمام موارد زیر باید در موتور صوتی User Mode اجرا شوند:

- Microphone Capture
- Pitch Shift
- Formant Processing
- Noise Gate
- Noise Suppression
- Equalizer
- Compressor
- Limiter
- Soundboard Playback
- Mixing
- Monitoring
- Metering
- Resampling

هیچ‌کدام از پردازش‌های DSP نباید داخل Kernel Driver انجام شوند.

## 3.2 وظیفهٔ درایور

درایور باید تا حد ممکن ساده باشد و فقط یک Virtual Audio Cable ایجاد کند.

درایور باید دو Endpoint بسازد:

```text
Playback endpoint:
GrassiBoard Virtual Cable Input

Recording endpoint:
GrassiBoard Virtual Microphone
```

اپلیکیشن خروجی نهایی را با WASAPI به `GrassiBoard Virtual Cable Input` ارسال می‌کند.

درایور همان جریان PCM را به برنامه‌هایی که `GrassiBoard Virtual Microphone` را باز کرده‌اند تحویل می‌دهد.

در صورت بسته بودن اپ:

- Virtual Microphone باید Silence تولید کند.
- نباید Noise، دادهٔ قدیمی یا صدای تکرارشونده تولید شود.
- نباید هیچ برنامه‌ای Hang کند.
- نباید Windows Audio Service کرش کند.

درایور را بر اساس نمونهٔ رسمی Microsoft SysVAD و معماری WaveRT توسعه بده؛ اما فرض نکن که SysVAD به‌صورت پیش‌فرض یک Virtual Cable کامل است. انتقال واقعی PCM بین Render Endpoint و Capture Endpoint باید جداگانه پیاده‌سازی و آزمایش شود.

## 3.3 موتور صوتی

موتور صوتی باید Native C++ باشد.

پیشنهاد:

```text
Language: C++20
API: WASAPI
Processing format: 32-bit float
Default sample rate: 48000 Hz
Initial microphone mode: Mono
Audio scheduling: Event-driven
Thread priority: MMCSS
Architecture: x64 only
```

الزامات Real-time Thread:

- هیچ Allocation حافظه در Audio Callback انجام نشود.
- هیچ Lock بلاک‌کننده‌ای استفاده نشود.
- فایل صوتی در Callback Decode نشود.
- Logging مستقیم از Audio Callback انجام نشود.
- از Lock-free SPSC Ring Buffer استفاده شود.
- Bufferهای پردازش از قبل Allocate شوند.
- Underflow و Overflow شمارش و گزارش شوند.
- Exception از مرز Audio Callback عبور نکند.

## 3.4 ارتباط UI و موتور صوتی

از C++/CLI استفاده نکن، مگر اینکه دلیل فنی مستندی وجود داشته باشد.

یک DLL با C ABI ایجاد کن و از WPF با P/Invoke آن را فراخوانی کن.

نمونهٔ API:

```text
gb_engine_create
gb_engine_destroy

gb_enumerate_input_devices
gb_enumerate_output_devices

gb_engine_start
gb_engine_stop

gb_set_input_device
gb_set_monitor_device
gb_set_virtual_output_device

gb_set_microphone_gain
gb_set_microphone_mute

gb_set_pitch_semitones
gb_set_pitch_cents
gb_set_formant_shift
gb_set_formant_preservation
gb_set_pitch_quality_mode
gb_set_pitch_bypass

gb_load_sound
gb_play_sound
gb_stop_sound
gb_stop_all_sounds
gb_set_sound_volume
gb_set_sound_loop

gb_get_input_meter
gb_get_output_meter
gb_get_cpu_load
gb_get_audio_statistics
```

API باید Versioned باشد تا تغییرات بعدی ABI را نشکنند.

---

# 4. ساختار Repository

ساختار پایه:

```text
GrassiBoard/
├── .github/
│   └── workflows/
│       ├── build.yml
│       ├── release.yml
│       └── driver-check.yml
│
├── src/
│   ├── GrassiBoard.App/
│   │   ├── WPF application
│   │   └── net8.0-windows
│   │
│   ├── GrassiBoard.AudioEngine/
│   │   ├── WASAPI capture
│   │   ├── WASAPI render
│   │   ├── DSP chain
│   │   ├── soundboard
│   │   └── C ABI
│   │
│   ├── GrassiBoard.Driver/
│   │   ├── SysVAD-derived driver
│   │   ├── INF
│   │   └── driver package project
│   │
│   └── GrassiBoard.Shared/
│       └── shared constants and identifiers
│
├── tests/
│   ├── GrassiBoard.AudioEngine.Tests/
│   ├── GrassiBoard.Dsp.Tests/
│   ├── GrassiBoard.DriverPackage.Tests/
│   └── test-assets/
│
├── tools/
│   ├── Install-GrassiBoardDriver.ps1
│   ├── Update-GrassiBoardDriver.ps1
│   ├── Uninstall-GrassiBoardDriver.ps1
│   ├── Install-TestCertificate.ps1
│   ├── Enable-TestSigning.ps1
│   ├── Collect-Diagnostics.ps1
│   └── Reset-DriverVerifier.ps1
│
├── installer/
│   └── packaging files
│
├── docs/
│   ├── architecture.md
│   ├── audio-pipeline.md
│   ├── driver-design.md
│   ├── test-plan.md
│   ├── troubleshooting.md
│   ├── recovery.md
│   └── current-status.md
│
├── CHANGELOG.md
├── README.md
├── LICENSES.md
├── Directory.Build.props
├── CMakePresets.json
└── GrassiBoard.sln
```

---

# 5. Audio Pipeline

ترتیب پایهٔ پردازش:

```text
Physical microphone capture
    ↓
Channel conversion
    ↓
DC blocker / high-pass filter
    ↓
Noise gate
    ↓
Optional noise suppression
    ↓
Pitch shift
    ↓
Formant processing
    ↓
Microphone EQ
    ↓
Compressor
    ↓
Microphone gain
    ↓
Microphone and soundboard mixer
    ↓
Master limiter
    ├── Virtual cable output
    └── Optional headset monitoring
```

Soundboard نباید به Pitch Shifter میکروفون وارد شود، مگر اینکه کاربر عمداً برای یک Sound Pad این گزینه را فعال کرده باشد.

---

# 6. Pitch و Formant

## 6.1 کنترل‌های UI

موارد زیر را اضافه کن:

```text
Pitch:
-12 تا +12 semitones در نسخهٔ اولیه

Fine pitch:
-100 تا +100 cents

Formant:
حداقل -6 تا +6

Pitch bypass:
On / Off

Formant preservation:
On / Off

Wet/Dry:
0 تا 100 درصد

Quality mode:
Low Latency
Balanced
High Quality
```

در نسخه‌های بعدی محدودهٔ Pitch می‌تواند تا ±24 semitones افزایش پیدا کند.

## 6.2 تغییر زنده

تغییر Slider نباید باعث موارد زیر شود:

- Click
- Pop
- قطع شدن صدا
- Reset شدن دستگاه
- قفل شدن UI

پارامترها باید Smoothing داشته باشند.

تغییر Pitch باید Ramp کوتاه داشته باشد و مقدار جدید مستقیماً در وسط Block با پرش ناگهانی اعمال نشود.

## 6.3 انتخاب موتور Pitch

از ابتدا یک Interface مستقل تعریف کن:

```text
IPitchProcessor
- Prepare
- Reset
- Process
- SetPitchSemitones
- SetFormant
- SetQualityMode
- GetLatencySamples
```

ابتدا چند گزینه را Benchmark کن، نه اینکه بدون تست یکی را دائمی انتخاب کنی.

حداقل گزینه‌های قابل بررسی:

1. Signalsmith Stretch
2. SoundTouch
3. یک الگوریتم Voice-specific مانند PSOLA در مرحلهٔ تحقیقاتی

برای هر موتور این موارد را اندازه بگیر:

- Algorithmic latency
- CPU usage
- Memory use
- Voice quality
- Metallic artifacts
- Transient handling
- Behaviour on silence
- Behaviour on unvoiced consonants
- کیفیت در Pitchهای ±3، ±6 و ±12 semitones
- پایداری هنگام تغییر زندهٔ Pitch

یک Backend را برای حالت Low Latency و در صورت نیاز Backend دیگری را برای High Quality نگه دار.

از نوشتن Pitch Shifter پیچیده از صفر پیش از تکمیل MVP خودداری کن.

## 6.4 اندازه‌گیری Latency

Latency را تخمین نزن؛ اندازه‌گیری کن.

موارد زیر جداگانه گزارش شوند:

```text
Physical capture buffer latency
Pitch algorithm latency
Mixer latency
Virtual render latency
Virtual capture latency
End-to-end latency
Monitoring latency
```

در UI صفحهٔ Diagnostics اضافه کن:

```text
Input buffer size
Output buffer size
Sample rate
Pitch latency in samples
Pitch latency in milliseconds
Ring buffer fill
Underrun count
Overrun count
Audio thread CPU
Total reported latency
```

---

# 7. Soundboard

هر Sound Pad باید این مشخصات را داشته باشد:

```text
ID
Title
Audio file
Volume
Pan
Hotkey
Playback mode
Loop
Restart on press
Stop previous
Fade in
Fade out
Ducking amount
Output to virtual mic
Output to monitoring
Color
Icon
```

حالت‌های پخش:

1. One Shot
2. Hold to Play
3. Toggle
4. Loop
5. Restart on Every Press
6. Allow Multiple Instances

فرمت‌های اولیه:

- WAV
- MP3
- FLAC
- OGG، در صورت قابل‌اعتماد بودن Decoder

فایل‌ها پیش از پخش باید Decode یا Cache شوند تا Audio Callback فایل را Decode نکند.

---

# 8. Mixer و Monitoring

کانال‌های اولیه:

```text
Microphone
Soundboard
Master
Monitoring
```

کنترل‌ها:

- Volume
- Mute
- Solo، در صورت نیاز
- Peak meter
- RMS meter
- Clipping indicator

Monitoring Mode:

```text
Off
Soundboard Only
Microphone and Soundboard
Custom Mix
```

صدای مانیتورینگ نباید دوباره وارد Virtual Microphone شود.

از Capturing ناخواستهٔ صدای Discord یا صدای مخاطب جلوگیری کن تا Echo Loop ایجاد نشود.

---

# 9. UI

UI با WPF ساخته شود.

نسخهٔ اولیه باید شامل این بخش‌ها باشد:

```text
Top bar:
Start / Stop Engine
Input device
Monitor device
Virtual output status
Latency status

Microphone panel:
Gain
Mute
Pitch
Fine pitch
Formant
Formant preservation
Wet/Dry
Noise gate
Compressor
Input meter

Soundboard panel:
Grid of pads
Add sound
Edit pad
Stop all
Page selector

Master panel:
Mic level
Soundboard level
Master level
Output meter
Clipping indicator

Diagnostics:
Sample rate
Buffer sizes
CPU usage
Dropouts
Driver status
Audio engine log
```

UI نباید Audio Engine را روی UI Thread اجرا کند.

تنظیمات UI از طریق Command Queue غیرمسدودکننده به Audio Engine ارسال شوند.

---

# 10. Profiles و Hotkeys

پروفایل باید این اطلاعات را ذخیره کند:

- دستگاه ورودی
- دستگاه Monitoring
- تنظیم Pitch
- تنظیم Formant
- تنظیم Noise Gate
- تنظیم Compressor
- Sound Pads
- Hotkeys
- ولوم‌ها
- Monitoring Mode
- اندازه و موقعیت پنجره

Global Hotkeyها:

```text
Play pad
Stop pad
Stop all
Mute microphone
Toggle pitch
Reset pitch
Push-to-talk
Show or hide application
```

Hotkey نباید هنگام تایپ عادی مزاحمت ایجاد کند و Conflict باید در UI نمایش داده شود.

---

# 11. GitHub Actions

## 11.1 Build Workflow

Workflow روی موارد زیر اجرا شود:

```text
Push to main
Pull request
workflow_dispatch
```

Runner را صریحاً Pin کن و از `windows-latest` بدون کنترل نسخه استفاده نکن.

Buildهای اولیه فقط برای:

```text
Platform: x64
Configuration: Release
Target OS: Windows 10/11 x64
```

مراحل Workflow:

1. Checkout repository
2. Restore submodules
3. Install or restore .NET SDK
4. Locate MSBuild
5. Restore NuGet packages
6. Restore pinned WDK NuGet package
7. Build Native Audio Engine
8. Run Native tests
9. Build WPF application
10. Run managed tests
11. Build driver
12. Validate INF
13. Generate driver catalog
14. Test-sign driver catalog
15. Verify signature
16. Publish WPF app as self-contained win-x64
17. Copy native DLLs
18. Copy driver package
19. Copy install and recovery scripts
20. Generate build information
21. Create ZIP packages
22. Upload artifacts

## 11.2 نسخه‌های ابزار

نسخه‌ها را Pin کن.

درایور از بستهٔ رسمی زیر استفاده کند:

```text
Microsoft.Windows.WDK.x64
```

برای Driver Project از MSBuild استفاده کن، نه `dotnet build`.

نسخهٔ WDK، SDK، .NET و Dependencies در فایل‌های پروژه مشخص و ثابت باشند.

Dependabot نباید بدون تست، نسخهٔ WDK یا کتابخانه‌های DSP را خودکار Merge کند.

## 11.3 خروجی Artifacts

در هر Push این خروجی‌ها را بساز:

```text
GrassiBoard-portable-win-x64-{version}-{shortsha}.zip
GrassiBoard-driver-x64-{version}-{shortsha}.zip
GrassiBoard-symbols-{version}-{shortsha}.zip
GrassiBoard-test-results-{version}-{shortsha}.zip
```

ZIP اصلی شامل موارد زیر باشد:

```text
GrassiBoard.exe
Native Audio Engine DLL
Third-party runtime DLLs
Driver package
Public test certificate
Install scripts
Uninstall scripts
Diagnostics script
README-FIRST.txt
CHANGELOG.md
BuildInfo.json
```

`BuildInfo.json` باید شامل این موارد باشد:

```text
Version
Commit SHA
Build date
Workflow run number
Configuration
Target architecture
WDK version
SDK version
.NET version
Pitch backend version
```

## 11.4 Releases

روی هر Push فقط Artifact بساز.

روی Git Tag به‌شکل زیر GitHub Release بساز:

```text
v0.1.0
v0.2.0
v1.0.0
```

نسخه‌های تأییدنشده را Prerelease علامت بزن.

## 11.5 Signing Secrets

کلید خصوصی یا PFX را هرگز داخل Repository قرار نده.

Secrets پیشنهادی:

```text
DRIVER_CERT_PFX_BASE64
DRIVER_CERT_PASSWORD
```

Public Certificate می‌تواند داخل Driver Package قرار بگیرد.

Workflow باید:

1. PFX را از GitHub Secret بازیابی کند.
2. آن را موقتاً وارد Certificate Store کند.
3. Driver Catalog را امضا کند.
4. Signature را Verify کند.
5. فایل PFX موقت را حذف کند.

Workflowهایی که Secrets مصرف می‌کنند نباید روی Pull Request ناشناس یا Fork اجرا شوند.

---

# 12. نصب روی سیستم کاربر

اسکریپت‌ها باید با Windows PowerShell استاندارد اجرا شوند و نصب Visual Studio یا WDK روی سیستم کاربر لازم نباشد.

## 12.1 راه‌اندازی یک‌باره

فایل زیر ایجاد شود:

```text
First-Time-Setup.ps1
```

این اسکریپت باید:

1. بررسی کند که با Administrator اجرا شده است.
2. وضعیت Secure Boot را تا حد امکان گزارش کند.
3. Test Signing را فعال کند.
4. گواهی عمومی تست را در Storeهای لازم نصب کند.
5. اعلام کند که Restart لازم است.
6. هیچ تنظیم امنیتی را بدون نمایش هشدار واضح تغییر ندهد.

## 12.2 نصب درایور

از `PnPUtil` استفاده کن.

اسکریپت نصب:

```text
Install-GrassiBoardDriver.ps1
```

وظایف:

1. بررسی Administrator
2. بررسی Test Signing
3. بررسی Certificate
4. نصب Driver Package
5. بررسی ایجاد Endpointها
6. ثبت نتیجه در Log
7. اعلام نیاز احتمالی به Restart

## 12.3 به‌روزرسانی

```text
Update-GrassiBoardDriver.ps1
```

باید:

1. اپ را ببندد.
2. نسخهٔ فعلی را شناسایی کند.
3. Driver Package جدید را Stage کند.
4. درایور قبلی را با روش امن به‌روزرسانی کند.
5. وضعیت Windows Audio Service را بررسی کند.
6. در صورت نیاز Restart پیشنهاد دهد.
7. امکان Rollback را حفظ کند.

## 12.4 حذف

```text
Uninstall-GrassiBoardDriver.ps1
```

باید:

- Device را حذف کند.
- Driver Package را از Driver Store حذف کند.
- فایل‌های باقی‌مانده را پاک کند.
- گواهی را فقط با تأیید صریح کاربر حذف کند.
- Test Signing را خودکار خاموش نکند، مگر با تأیید کاربر.

---

# 13. Logging و Diagnostics

Log نباید محتوای خام صدای کاربر را ذخیره کند.

موارد قابل ثبت:

```text
Device names
Device IDs
Sample formats
Buffer sizes
Engine state changes
Pitch backend
Latency reports
Underruns
Overruns
Driver install result
HRESULT values
Exceptions
Crash information
Build information
```

اسکریپت زیر ایجاد شود:

```text
Collect-Diagnostics.ps1
```

خروجی:

```text
GrassiBoard-Diagnostics-{date}.zip
```

اطلاعات حساس مانند نام کاربری، مسیرهای شخصی و فایل‌های Soundboard تا حد ممکن Redact شوند.

---

# 14. تست‌ها

## 14.1 Unit Tests

برای DSP:

- Silence input
- Impulse input
- Sine wave
- Pink noise
- Speech fixture
- Very small blocks
- Variable block sizes
- Reset during processing
- Pitch bypass
- Rapid pitch automation
- NaN and infinity protection

## 14.2 Pitch Tests

برای مقادیر زیر خروجی تست تولید کن:

```text
-12 semitones
-6 semitones
-3 semitones
0
+3 semitones
+6 semitones
+12 semitones
```

موارد بررسی:

- طول فایل ثابت بماند.
- Sample count منطقی بماند.
- خروجی NaN نداشته باشد.
- Peak غیرمجاز نداشته باشد.
- Silence به Noise تبدیل نشود.
- تغییر Pitch قابل اندازه‌گیری باشد.
- Bypass تا حد ممکن Null Test را پاس کند.
- Formant Preservation قابل خاموش و روشن کردن باشد.

## 14.3 Driver Package Tests

- INF validation
- CAT generation
- Signature verification
- Stable endpoint GUIDs
- Correct friendly names
- x64 package completeness
- Version increment
- Uninstall information
- Silence behavior when app is closed

## 14.4 تست دستی کاربر

پس از هر نسخه، فقط تست‌های همان Milestone را درخواست کن.

فرمت پاسخ تست:

```text
Version:
Windows version:
USB headset:
Driver installed: Yes/No
Virtual microphone visible: Yes/No
App opened: Yes/No
Audio passed: Yes/No
Pitch worked: Yes/No
Estimated delay:
Crackling or dropout:
Steps that caused the problem:
Screenshots or logs:
```

---

# 15. ایمنی و Recovery

Driver Development ممکن است باعث اختلال صوتی یا BSOD شود.

فایل `docs/recovery.md` باید شامل موارد زیر باشد:

- ورود به Safe Mode
- حذف Driver Package
- استفاده از PnPUtil
- غیرفعال کردن Driver Verifier
- برگرداندن Windows Audio Service
- حذف Test Certificate
- خاموش کردن Test Signing
- محل `setupapi.dev.log`
- محل Crash Dump
- روش بازگشت به آخرین نسخهٔ سالم

Driver Verifier فقط در Milestoneهای پایانی و با هشدار واضح اجرا شود.

هیچ اسکریپتی نباید Driver Verifier را بدون تأیید کاربر روی تمام Driverهای سیستم فعال کند.

---

# 16. Milestoneها

## Milestone 0 — Repository و CI پایه

نسخه: `v0.1.0`

وظایف:

- ساخت Repository Structure
- ساخت WPF shell
- ساخت Native DLL آزمایشی
- برقراری P/Invoke
- ساخت Driver placeholder project
- ساخت GitHub Actions
- تولید Artifact
- افزودن BuildInfo
- افزودن README و Changelog

معیار پذیرش:

- Workflow سبز باشد.
- اپ باز شود.
- نسخه و Commit را نمایش دهد.
- DLL Native را Load کند.
- ZIP قابل دانلود تولید شود.
- هنوز هیچ Audio Processing لازم نیست.

پس از تولید نسخه متوقف شو و منتظر تست کاربر بمان.

---

## Milestone 1 — Physical Microphone Passthrough

نسخه: `v0.2.0`

وظایف:

- Enumerate کردن Microphoneها
- Enumerate کردن Playback Deviceها
- انتخاب USB Headset Microphone
- Capture با WASAPI
- Monitoring به USB Headset
- Start و Stop امن
- Input و Output Meter
- ثبت Buffer Size و Dropout
- بدون Pitch و بدون Driver

معیار پذیرش:

- کاربر صدای میکروفون را در هدست بشنود.
- Start و Stop بدون کرش کار کند.
- تغییر Device قابل انجام باشد.
- UI قفل نشود.
- Meterها زنده باشند.

پس از تست کاربر متوقف شو.

---

## Milestone 2 — Pitch Shift Prototype

نسخه: `v0.3.0`

وظایف:

- اضافه کردن `IPitchProcessor`
- پیاده‌سازی اولین Pitch Backend
- Pitch از -12 تا +12
- Fine Pitch
- Bypass
- Parameter Smoothing
- Latency Reporting
- تست روی فایل‌های صوتی
- تولید Sample Output برای مقایسه

معیار پذیرش:

- Pitch در لحظه تغییر کند.
- سرعت صحبت تغییر نکند.
- Click و Pop شدید وجود نداشته باشد.
- در حالت Bypass صدا سالم باشد.
- Latency الگوریتم گزارش شود.

پس از تست متوقف شو.

---

## Milestone 3 — Formant و مقایسهٔ Backendها

نسخه: `v0.4.0`

وظایف:

- Formant Preservation
- Formant Shift
- Low Latency Mode
- Balanced Mode
- High Quality Mode
- مقایسهٔ حداقل دو Backend یا دو Configuration
- Benchmark CPU و Latency
- ذخیرهٔ نتایج در `docs/pitch-benchmark.md`

معیار پذیرش:

- کاربر بتواند بین حالت سریع و باکیفیت انتخاب کند.
- Formant روشن و خاموش تفاوت قابل شنیدن داشته باشد.
- تغییر تنظیمات باعث قطع Stream نشود.
- Backend پیش‌فرض بر اساس تست انتخاب شود.

پس از تست متوقف شو.

---

## Milestone 4 — Virtual Driver Skeleton

نسخه: `v0.5.0`

وظایف:

- Fork یا استخراج حداقلی بخش‌های لازم از SysVAD
- تغییر نام‌ها، GUIDها و Hardware IDs
- ساخت Endpointهای Render و Capture
- ساخت INF و CAT
- Test Signing
- اسکریپت نصب و حذف
- GitHub Actions Driver Artifact

معیار پذیرش:

- Driver روی سیستم کاربر نصب شود.
- دو Endpoint نمایش داده شوند.
- Device Manager خطا نشان ندهد.
- Windows Audio Service پایدار بماند.
- حذف درایور ممکن باشد.

در این Milestone هنوز انتقال کامل صدا الزامی نیست.

پس از تست متوقف شو.

---

## Milestone 5 — Virtual Cable PCM Transport

نسخه: `v0.6.0`

وظایف:

- ایجاد Ring Buffer انتقال PCM
- دریافت PCM از Render Endpoint
- تحویل PCM به Capture Endpoint
- مدیریت Clock و Position
- مدیریت Silence
- مدیریت Overrun و Underrun
- تست با یک فایل WAV ثابت
- عدم وابستگی به GrassiBoard App برای تست کابل

معیار پذیرش:

1. یک فایل صوتی به `GrassiBoard Virtual Cable Input` پخش شود.
2. همان صدا از `GrassiBoard Virtual Microphone` قابل ضبط باشد.
3. قطع Playback باعث Silence شود.
4. صدای قدیمی Loop نشود.
5. Windows Audio Service کرش نکند.
6. کیفیت فایل ضبط‌شده قابل قبول باشد.

پس از تست متوقف شو.

---

## Milestone 6 — اتصال Audio Engine به Virtual Microphone

نسخه: `v0.7.0`

وظایف:

- خروجی موتور به Virtual Cable Input ارسال شود.
- Pitch روی Virtual Microphone شنیده شود.
- Monitoring جداگانه باقی بماند.
- انتخاب Virtual Output در UI
- تشخیص خودکار Driver
- نمایش Driver Status
- تست در Windows Voice Recorder و OBS

معیار پذیرش:

- صدای پردازش‌شده در Voice Recorder ضبط شود.
- Pitch در خروجی Virtual Mic وجود داشته باشد.
- مانیتورینگ باعث Loop نشود.
- اپ مقصد بتواند Virtual Mic را باز کند.

پس از تست متوقف شو.

---

## Milestone 7 — Soundboard

نسخه: `v0.8.0`

وظایف:

- Sound Pad Grid
- Drag and Drop
- WAV و MP3
- Volume
- Loop
- Stop All
- چند صدای هم‌زمان
- پخش در Virtual Mic
- پخش اختیاری در Monitoring
- Cache یا Predecode

معیار پذیرش:

- صداهای Soundboard با میکروفون Mix شوند.
- میکروفون هنگام پخش Soundboard قطع نشود.
- Stop All فوری کار کند.
- پخش Pad باعث Dropout محسوس نشود.

پس از تست متوقف شو.

---

## Milestone 8 — Mixer و Dynamic Processing

نسخه: `v0.9.0`

وظایف:

- Mic Gain
- Soundboard Gain
- Master Gain
- Noise Gate
- Compressor
- Limiter
- Ducking
- Clipping Protection
- Wet/Dry Pitch Mix
- Presetهای صوتی

معیار پذیرش:

- Clipping کنترل شود.
- تغییر تنظیمات زنده باشد.
- Ducking قابل تنظیم باشد.
- هیچ کنترل UI، Audio Thread را Block نکند.

پس از تست متوقف شو.

---

## Milestone 9 — Profiles، Hotkeys و Tray

نسخه: `v0.10.0`

وظایف:

- Profiles
- Auto-save
- Global Hotkeys
- System Tray
- Start minimized
- Start with Windows، اختیاری
- Push-to-talk
- Mute hotkey
- Pitch toggle
- Reset pitch
- Soundboard page switching

معیار پذیرش:

- Hotkeyها هنگام Minimize بودن کار کنند.
- Profile بعد از Restart بازیابی شود.
- Device گمشده باعث کرش نشود.
- Conflict Hotkey نمایش داده شود.

پس از تست متوقف شو.

---

## Milestone 10 — Latency Optimization

نسخه: `v0.11.0`

وظایف:

- تست Bufferهای مختلف
- WASAPI Event-driven
- بررسی Shared و Exclusive Capture
- MMCSS
- حذف Allocationها
- Performance Profiling
- کاهش Resampling
- اندازه‌گیری End-to-end Latency
- ایجاد Low Latency Wizard

معیار پذیرش:

- حالت پایدار برای هدست کاربر پیدا شود.
- Dropout Counter در تست طولانی افزایش غیرعادی نداشته باشد.
- Latency قبل و بعد مستند شود.
- هیچ کاهش Latency بدون تست پایداری پذیرفته نشود.

پس از تست متوقف شو.

---

## Milestone 11 — Stability Test

نسخه: `v0.12.0`

وظایف:

- تست اجرای طولانی
- تعویض Device
- قطع و وصل USB Headset
- Sleep و Resume
- Restart Windows Audio Service
- باز و بسته شدن برنامهٔ مقصد
- Crash recovery
- Driver Verifier محدود به Driver پروژه
- Memory leak test
- Handle leak test

معیار پذیرش:

- اجرای چندساعته بدون Crash
- بازگشت مناسب پس از قطع و وصل USB
- امکان Restart کردن Engine بدون Restart ویندوز
- نبود Memory Leak جدی
- نبود BSOD در تست تأییدشده

پس از تست متوقف شو.

---

## Milestone 12 — Packaging و نسخهٔ پایدار

نسخه: `v1.0.0`

وظایف:

- Portable Package کامل
- First-time setup
- Installer یا Bootstrapper اختیاری
- Driver update
- Driver rollback
- Uninstaller
- Documentation نهایی
- Known Issues
- Recovery Guide
- Release Workflow
- GitHub Release

معیار پذیرش:

- کاربر فقط فایل خروجی GitHub را دانلود کند.
- نصب اولیه با دستورالعمل مشخص انجام شود.
- Visual Studio، WDK یا SDK روی سیستم کاربر لازم نباشد.
- درایور نصب و اپ اجرا شود.
- Virtual Microphone در برنامهٔ مقصد قابل انتخاب باشد.
- Pitch و Soundboard هم‌زمان کار کنند.

---

# 17. تعریف پایان پروژه

پروژه زمانی به نسخهٔ 1.0 می‌رسد که:

1. صدای USB Microphone بدون قطع دریافت شود.
2. Pitch و Formant زنده کار کنند.
3. سرعت صحبت ثابت بماند.
4. Soundboard با میکروفون Mix شود.
5. خروجی در Virtual Microphone دیده شود.
6. OBS، مرورگر یا Discord بتواند آن را استفاده کند.
7. Monitoring اختیاری و بدون Echo Loop باشد.
8. GitHub Actions بعد از هر Push خروجی بسازد.
9. کاربر برای Build به ابزار محلی نیاز نداشته باشد.
10. نصب، حذف و Recovery درایور مستند و قابل انجام باشد.
11. نسخهٔ نهایی در تست طولانی پایدار باشد.

---

# 18. اولین اقدام

در اولین پاسخ:

1. Repository موجود را بررسی کن.
2. اگر خالی است، فقط Milestone 0 را شروع کن.
3. Architecture و File Structure را بساز.
4. Workflow اولیه را اضافه کن.
5. نسخهٔ `v0.1.0` را تولید کن.
6. Artifact قابل دانلود بساز.
7. دستور تست نسخهٔ `v0.1.0` را ارائه کن.
8. قبل از دریافت نتیجهٔ تست کاربر وارد Milestone 1 نشو.
