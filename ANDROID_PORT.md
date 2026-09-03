# Biformis — Hồ sơ chuyển sang Android

Tài liệu này phục vụ hai người đọc:

1. **Đội dev bản PC** — biết đâu là những chỗ nếu động vào sẽ làm hỏng bản Android, và biết những bug chung đã được sửa.
2. **Người/AI làm bản Android lần sau** — dựng lại được toàn bộ quyết định mà không cần đọc lại lịch sử chat.

Cập nhật lần cuối: 2026-08-29. Unity 6000.3.10f1, URP 2D, Input System 1.18 (chỉ New Input System — `activeInputHandler: 1`).

---

## 1. Nguyên tắc cốt lõi

> **Touch giả lập một Gamepad ảo, không sửa một dòng gameplay code nào.**

Mỗi hệ thống trong project tự tạo instance riêng (`new InputSystemActions()`) thay vì dùng chung một `PlayerInput`. Vì vậy **không thể** can thiệp ở tầng action. Cách duy nhất không phải sửa gameplay là giả lập ở **tầng thiết bị**: `OnScreenControl` của Unity tạo ra một Gamepad ảo, bơm giá trị vào các control path mà action **vốn đã bind sẵn**.

Hệ quả quan trọng: **touch đi đúng con đường mà bàn phím đi**. Không có nhánh code riêng cho mobile, nên gameplay không thể lệch giữa hai bản.

Toàn bộ HUD được **dựng bằng code lúc runtime** — không prefab, không sprite import, không sửa scene. Nhờ vậy danh sách scene và thứ tự load giữ nguyên tuyệt đối (`EditorBuildSettings.asset` và `InputSystem_Actions.inputactions` đều không có diff).

---

## 2. Bảng ánh xạ điều khiển

| Hành động | Phím PC | Control path giả lập | Nút mobile |
|---|---|---|---|
| Di chuyển | WASD / mũi tên | `<Gamepad>/leftStick` | Joystick nổi, nửa trái |
| Sprint (giữ) + Dash (chạm) | `LeftShift` | `<Gamepad>/leftStickPress` | Nút phải dưới |
| Đổi dimension | `J` | `<Gamepad>/rightShoulder` | Nút góc phải dưới |
| Pause | `Esc` | `<Gamepad>/buttonEast` | Nút góc phải trên |
| Tương tác | `E` | `<Gamepad>/buttonNorth` | *(tắt mặc định)* |

Định nghĩa tại `Assets/_Scripts/CaptainPinkTurd/Core/Input Paths/MobileControlPaths.cs`.

### Vì sao chọn những path đó

- **`leftStickPress`** đã được bind sẵn cho **cả** `Run` và `Dash` trong `.inputactions`, y hệt `LeftShift`. Nên một nút cho ra đúng hành vi PC: giữ = sprint, chạm = dash. Không cần code tách biệt.
- **`buttonEast`** mang usage `"Cancel"`, mà action `UI/Cancel` bind theo kiểu usage (`*/{Cancel}`) — chính là action `Esc` dùng. Nên nút pause chạy đúng vào `PopupActivator` có sẵn, ra đúng menu, đóng băng thời gian đúng cách. `Player/Crouch` cũng bind `buttonEast` nhưng **không hệ thống nào đọc nó**, nên không xung đột.
- **`rightShoulder`** được chọn vì không action nào khác dùng. Đổi dimension là `InputAction` tạo trong code tại `GameManager.Awake()` chứ không nằm trong `.inputactions`, nên phải `AddBinding()` thêm ở đó.

---

## 3. File đã thêm

```
Assets/_Scripts/CaptainPinkTurd/
├─ Core/Input Paths/
│  └─ MobileControlPaths.cs         Hằng số control path, dùng chung Game ↔ MobileControls
├─ Core/Rendering/
│  └─ CameraFraming.cs              Sửa khung hình cho màn không phải 16:9
└─ Mobile Controls System/
   ├─ MobileControlsSystem.asmdef
   ├─ MobileControlsHUD.cs          Dựng canvas, quản lý hiển thị
   ├─ FloatingOnScreenStick.cs      Joystick nổi (Unity không có sẵn loại này)
   ├─ MobileHudButton.cs            Phản hồi hình ảnh khi bấm (thuần thẩm mỹ)
   ├─ MobileControlGraphics.cs      Vẽ sprite nút bằng code, khỏi cần import ảnh
   ├─ SafeAreaFitter.cs             Tránh tai thỏ / thanh cử chỉ
   └─ Tests/                        9 PlayMode test
```

## 4. File có sẵn đã bị sửa

| File | Sửa gì | Ảnh hưởng bản PC |
|---|---|---|
| `GameManager.cs` | Thêm binding thứ 2 cho đổi dimension | Không — phím `J` giữ nguyên |
| `HitStop.cs` | Không lưu timescale đã đóng băng; thêm `Abort()` | **Sửa bug PC** |
| `ShakeUtils.cs` | Cùng lỗi; `OnComplete` → `OnKill` | **Sửa bug PC** |
| `SceneController.cs` | Gọi `HitStop.Abort()` khi chuyển scene | **Sửa bug PC** |
| `PlayerAnimationController.cs` | Chọn hướng bằng dot product; không phát lại animation đang chạy | **Sửa bug PC** (phần animation) |
| `AnimationControllerBase.cs` | Giải phóng timer cũ trước khi tạo timer mới | **Sửa rò rỉ PC** |
| `Move_Effect.cs` | Trail bật khi sprint, không chỉ khi dash | **Sửa đúng ý đồ gốc** (xem §6) |
| `Renderer2D.asset` | Bật Camera Sorting Layer Texture, bound = 8 | **Sửa bug PC ẩn** |
| `Core.unity` | Loading overlay stretch full màn | Không — 16:9 vốn đã che kín |
| `Player TopDown Movement Stats.asset` | `walkSpeed` 6.5, `runSpeed` 8.45 | Có — đây là cân bằng game |
| `ProjectSettings.asset` | Khóa landscape, package name Android | Không |

---

## 5. Bug đã tìm ra — phần lớn là bug của bản PC

Đây là phần quan trọng nhất với đội PC. Những lỗi này **đã tồn tại từ trước**, chỉ là Android làm chúng lộ ra.

### 5.1 Đóng băng vĩnh viễn sau khi restart ⚠️ nghiêm trọng nhất

**Triệu chứng:** sau vài lần restart (tự bấm hoặc game over tự restart), nhân vật vẫn quay mặt theo joystick/WASD nhưng không di chuyển được nữa.

**Vì sao triệu chứng lại kỳ lạ như vậy:** `Time.timeScale == 0` khiến `Update()` vẫn chạy (nên `movementInput` vẫn cập nhật → sprite vẫn lật) nhưng `FixedUpdate()` **không** chạy (nên `rb.linearVelocity` không bao giờ được gán). Nhớ dấu hiệu này: **quay được nhưng không đi = timeScale bằng 0.**

**Hai nguyên nhân trong `HitStop`:**

1. `oldTimeScale = Time.timeScale` rồi cuối `WaitLoop` ghi trả lại. Nếu lúc gọi `Stop()` mà timescale đã là 0 sẵn — popup pause và popup game over đều set 0 qua `PopupManager.SetTimeScale(0)` — nó lưu số 0 và **trả lại 0 vĩnh viễn**.
2. Runner là `DontDestroyOnLoad` và đếm bằng **unscaled time**, nên hit-stop bắt đầu ngay trước lúc chuyển scene vẫn sống qua transition, rồi ghi đè timescale của màn mới. `SceneController` có set `timeScale = 1` nhưng hit-stop cũ kết thúc **sau** đó.

**Đã sửa:** `Stop()` quy mọi timescale ≤ 0 về 1; thêm `HitStop.Abort()` gọi từ `SceneController` ngay chỗ đã ép `timeScale = 1`.

`ShakeUtils.SynchronizedShakeWithProfile` dính đúng lỗi cùng loại, cộng thêm việc khôi phục timescale trong `OnComplete` của DOTween — callback này **không chạy** nếu tween bị hủy giữa chừng (object destroy, scene unload), để lại game đóng băng vĩnh viễn. Đổi sang `OnKill` (chạy cả khi hoàn thành lẫn khi bị hủy).

Có 3 test hồi quy trong `Tests/TimeScaleRegressionTests.cs`.

### 5.2 Shockwave làm trắng/xám màn hình

`Shockwave_Screen_Vfx` (dùng bởi Spider, Turret, Bullet Source) lấy mẫu `_CameraSortingLayerTexture`, nhưng `Renderer2D.asset` để `m_UseCameraSortingLayersTexture: 0`. DX11 trên PC che được lỗi này, Vulkan/GLES trên Android trả về xám đặc.

**Đã sửa:** bật lên, và đặt `m_CameraSortingLayersTextureBound: 8` (= `Default`). Con số này quan trọng: sprite shockwave nằm ở sorting layer `CameraSortingLayer` (index 9), nên texture phải chụp mọi layer **bên dưới** nó, tức 0→8. Nếu để 0 thì nó chỉ bóp méo mỗi layer `Outside map`.

> ⚠️ **Chưa kiểm chứng bằng mắt.** Nếu thêm/bớt sorting layer trong `TagManager.asset`, phải chỉnh lại con số này.

### 5.3 Hướng nhân vật sai khi dùng joystick

`PlayerAnimationController.OnMovementInputChangeEvent` so sánh `dir.ToVector2() != input` — **so sánh float tuyệt đối** với các vector cardinal. Bàn phím qua composite Dpad cho ra đúng `(1,0)` nên PC không lộ. Joystick analog cho `(0.998, 0.021)`, không khớp hướng nào, nên `playerCurrentDirectionState` không bao giờ đổi → nhân vật đi sang phải mà vẫn quay mặt sang trái.

**Đã sửa:** chọn hướng gần nhất bằng dot product. Bàn phím vẫn ra cardinal chính xác nên hành vi PC không đổi.

### 5.4 Sprint không có trail

`Move_Effect.cs` có field tên `runTrail` và comment ghi *"Enable trail only when Run (Shift) is pressed"*, nhưng lại nối vào `isPlayerDashing`. Code đã lệch khỏi ý đồ thiết kế từ trước.

**Đã sửa:** trail bật khi `isDashing` **hoặc** đang giữ `Run` và thực sự di chuyển. Đọc action `Run` trực tiếp thay vì bám vào `isDashing`, vì cờ đó còn kéo theo `invincibilityLayer` trong `PlayerUnit` — bám vào nó thì sprint sẽ được bất tử, không phải ý đồ.

### 5.5 Nhân vật mất animation khi di chuyển ⚠️

**Triệu chứng:** vào game còn thấy animation, chỉ cần đẩy joystick một chút là nhân vật đứng hình, trông rất đơ.

`AnimationControllerBase.PlayAnimation()` **không có guard chống gọi lặp**. Mỗi lần gọi nó chạy `animator.CrossFade(hash, crossfadeTime, 0, 0f)` — tham số cuối là normalizedTime, `0f` nghĩa là **tua clip về frame đầu**.

`PlayerAnimationController` gọi `SetPlayerWalkAnimation()` trong mỗi lần `movementInput` đổi giá trị. Bàn phím dùng composite Dpad nên giá trị chỉ đổi lúc bấm/nhả phím — gọi vài lần, không ai để ý. Joystick analog đổi giá trị **mỗi frame** → animation bị tua về frame 0 mỗi frame → ghim cứng ở khung hình đầu.

**Đã sửa:** `PlayerAnimationController` nhớ hash đang phát và bỏ qua nếu trùng.

**Kèm theo một rò rỉ nghiêm trọng.** `Timer.Start()` đăng ký timer vào **danh sách static** của `TimerManager`. `PlayAnimation` gán đè `timer` mà không giải phóng cái cũ, nên mỗi lần gọi là một timer bị bỏ rơi nhưng **vẫn nằm trong danh sách tick mỗi frame vĩnh viễn**. Đi bộ 30 giây ở 60fps là rò khoảng 1800 timer. Tệ hơn nữa: `OnTimerStop` của chúng vẫn nổ về sau và `CrossFade` về `DefaultAnimationHash`, đánh nhau với animation đang phát.

**Đã sửa:** `PlayAnimation` gọi `timer?.Dispose()` trước khi tạo timer mới. `Dispose` chỉ deregister chứ không kích hoạt `OnTimerStop`, nên không sinh callback lạc.

> Đây là bug PC luôn, chỉ là bàn phím rời rạc nên không lộ. Bất cứ controller nào đổi animation giữa chừng đều đang rò timer.

### 5.6 Loading overlay không che kín màn

Ảnh overlay đen là RectTransform cố định `640×360` đặt giữa. Trên 16:9 nó vừa khít nên PC không lộ. Trên 20:9 canvas rộng ~715 đơn vị → hở hai bên ~5%.

**Đã sửa:** anchor stretch `(0,0)-(1,1)`, sizeDelta `(0,0)`.

---

## 6. Sai lầm tôi đã mắc — ghi lại để lần sau không lặp

Trung thực về những thứ đã làm sai và mất thời gian:

**Guard `LensSettings.Orthographic`.** Bản `CameraFraming` đầu tiên duyệt các Cinemachine vcam và bỏ qua cái nào không orthographic. Nhưng `LensSettings.Orthographic` được Cinemachine **set mỗi frame từ Unity camera**, nên lúc scene vừa load nó trả `false` → bỏ qua toàn bộ vcam → không sửa được gì. Bản hiện tại bỏ hẳn phụ thuộc Cinemachine, ghi thẳng vào `Camera.orthographicSize` từ `LateUpdate` với `[DefaultExecutionOrder(10000)]` (CinemachineBrain không khai báo execution order nên là 0, chạy trước).

**Dùng `Time.timeScale == 0` làm tín hiệu "đang mở menu".** Sai vì `HitStop` cũng zero timescale mỗi lần trúng đòn. Hậu quả: mỗi va chạm là joystick bị `SetActive(false)` → mất pointer → người chơi phải nhấc tay lên mới điều khiển lại được. Bản hiện tại hỏi thẳng `PopupManager.AnyPopupShowing()`, và dùng `CanvasGroup` thay `SetActive` để không bao giờ disable control giữa chừng.

**Kết luận sai rằng `PopupManager` không có trong scene nào.** Tôi grep theo script GUID — sai phương pháp, vì **prefab instance trong scene không lặp lại GUID của script**, chỉ có override. Thực tế `Popup Manager.prefab` có trong cả 12 level và đã nối sẵn đầy đủ. Bài học: muốn biết prefab có trong scene không thì grep **GUID của prefab**, không phải của script.

**Đề xuất nút Interact khi chưa kiểm chứng.** Tôi tư vấn thêm nút `E` vì tưởng cửa/NPC cần. Thực tế `Door` mở bằng cách **đi lên trên** (`playerInput.y > 0`), và `InteractionDetector2D` **không được gắn ở scene nào**. Nút vẫn được code đầy đủ nhưng để `showInteractButton = false`.

**Test multi-touch không chạy được.** Đã thử 3 cách bơm sự kiện Touchscreen ảo, đều không tới được device dưới `-batchmode` (`press=false, position=0`) dù raycast trúng đúng nút và module active. Đã bỏ test đó thay vì để nó đỏ vô nghĩa. **Multi-touch vẫn chưa được kiểm chứng tự động** — phải thử tay.

---

## 7. Cạm bẫy cho đội dev bản PC

Những thứ dưới đây nếu đổi sẽ **âm thầm làm hỏng bản Android** mà PC vẫn chạy bình thường.

### Đừng gỡ các binding Gamepad trong `InputSystem_Actions.inputactions`

Nút cảm ứng bơm vào chính những path đó. Gỡ `<Gamepad>/leftStickPress` khỏi `Run` là nút sprint trên mobile chết ngay, mà PC không hề hấn gì.

### Đừng đổi cách đặt tên scene level

`MobileControlsHUD.IsGameplaySceneLoaded()` hiện/ẩn HUD dựa vào `scene.name.StartsWith("Level")` (hằng `SceneDatabase.Scenes.Level`). Scene chơi được mà không bắt đầu bằng `Level` sẽ không có HUD.

### Thêm phím mới = phải thêm nút mobile

Thêm một hành động dùng phím trên PC thì trên mobile hoàn toàn không có cách nào kích hoạt. Quy trình: bind thêm một control Gamepad chưa dùng → thêm hằng vào `MobileControlPaths` → thêm `BuildButton(...)` trong `MobileControlsHUD.Build()`.

Các control Gamepad **thật sự còn trống**: `leftShoulder`, `leftTrigger`, `rightTrigger`, `rightStickPress`, `start`, `select`.

Đã bị chiếm — **đừng** dùng lại:

| Control | Bị chiếm bởi |
|---|---|
| `leftStick` | `Move` + `UI/Navigate` |
| `leftStickPress` | `Run` **và** `Dash` |
| `rightStick` | `Look` + `UI/Navigate` |
| `dpad`, `dpad/left`, `dpad/right` | `UI/Navigate`, `Previous`, `Next` |
| `buttonSouth` | `Jump`, và mang usage `Submit` → `UI/Submit` |
| `buttonEast` | `Crouch`, và mang usage `Cancel` → `UI/Cancel` (nút pause dùng) |
| `buttonNorth` | `Interact` |
| `buttonWest` | `Attack` |
| `rightShoulder` | Đổi dimension (bản port thêm vào) |

Lưu ý: `Jump`, `Next`, `Previous`, `Attack`, `Look` **không có hệ thống nào đọc** trong gameplay hiện tại — nhưng control của chúng vẫn bị binding chiếm, và `buttonSouth`/`buttonEast` còn mang usage mà UI đang dùng thật. Muốn tái sử dụng thì phải gỡ binding trước, và nhớ là gỡ `buttonEast` sẽ làm chết nút pause mobile.

### Cẩn thận với `Time.timeScale`

Đừng viết `float old = Time.timeScale; Time.timeScale = 0; ... Time.timeScale = old;`. Nếu lúc chạy timescale đã là 0 thì bạn vừa đóng băng game vĩnh viễn. Luôn quy về 1: `Time.timeScale > 0f ? Time.timeScale : 1f`. Và nếu khôi phục trong callback bất đồng bộ, hãy chắc callback đó chạy cả khi bị hủy (DOTween: dùng `OnKill`, không phải `OnComplete`).

### UI phải dùng anchor stretch, đừng dùng size cố định

Bất cứ ảnh nào định che kín màn hình phải để anchor `(0,0)-(1,1)` với offset 0. Kích thước cố định `640×360` vừa khít 16:9 nhưng hở trên mọi tỉ lệ điện thoại.

### Sorting layer

Nếu thêm/bớt/đổi thứ tự sorting layer, phải chỉnh lại `m_CameraSortingLayersTextureBound` trong `Renderer2D.asset` (§5.2).

---

## 8. Cách khung hình xử lý tỉ lệ màn

`CameraFraming` giữ **bề ngang thế giới đúng bằng bản PC**, cắt bớt trên/dưới trên màn cao hơn:

```
orthographicSize = min(authored, authored × (16/9) / aspect)
```

| Màn | Tỉ lệ | Ortho size | Kết quả |
|---|---|---|---|
| PC 16:9 | 1.78 | 5.5 | **không đổi** |
| 18:9 | 2.00 | 4.89 | cắt trên/dưới |
| 19.5:9 iPhone | 2.17 | 4.51 | cắt trên/dưới |
| 20:9 Xiaomi | 2.22 | 4.40 | cắt trên/dưới |
| 21:9 | 2.33 | 4.19 | cắt trên/dưới |
| Tablet 4:3 | 1.33 | 5.5 | không đổi |

Camera orthographic cố định **chiều dọc**, nên màn càng rộng càng thấy nhiều theo chiều ngang — đó là lý do bản đầu tiên lòi ra ngoài map. Công thức trên đảo lại: cố định chiều ngang.

`Mathf.Min` là cố ý: màn **hẹp hơn** 16:9 (tablet) sẽ không được phóng to, vì phóng to sẽ lòi ra ngoài map theo chiều dọc. Đánh đổi: tablet thấy ít hơn bản PC ~25% theo chiều ngang. Muốn hỗ trợ tablet tử tế thì cách đúng là pillarbox (viền đen hai bên) để giữ chính xác 16:9.

---

## 9. Build Android

Toolchain đi kèm Unity 6000.3.10f1 đã đủ, không cần cài thêm: SDK android-34/35/36, build-tools 36.0.0, NDK 27.2.12479018, OpenJDK.

**Cấu hình hiện tại:** IL2CPP, ARM64 only, minSdk 25, targetSdk auto (36), landscape hai chiều, `androidUseCustomKeystore: 0` (Unity tự dùng debug keystore → build được ngay, không cần tạo keystore).

⚠️ **ARM64 only** — máy ARM 32-bit sẽ không cài được. Kiểm tra:

```bash
ADB="/c/Program Files/Unity/Hub/Editor/6000.3.10f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe"
"$ADB" shell getprop ro.product.cpu.abi     # cần ra arm64-v8a
```

**Các bước:** bật USB debugging → `File > Build Profiles` → tick **Development Build** cho lần test đầu → **bỏ tick** Build App Bundle nếu muốn file `.apk` → Build And Run. Lần đầu IL2CPP compile 10–25 phút.

**Đọc log:** `"$ADB" logcat -s Unity`

⚠️ **Package name đang là `com.DefaultCompany.GameNameJamProject`** — phải đổi trước khi lên Play Store.

Đổi build target sang Android cũng khiến Unity tự sinh vài thay đổi trong `ProjectSettings.asset` (platform icon rỗng, static batching, define `MOREMOUNTAINS_NICEVIBRATIONS_INSTALLED`). Đó là Unity tự làm, không phải chỉnh tay.

---

## 10. Chạy test

9 PlayMode test, chạy headless không cần máy thật. **Phải đóng Unity Editor trước** (tranh lock).

```bash
"/c/Program Files/Unity/Hub/Editor/6000.3.10f1/Editor/Unity.exe" \
  -batchmode -projectPath "D:/biformis_android" \
  -runTests -testPlatform PlayMode \
  -testFilter "CaptainPinkTurd.MobileControls.Tests*" \
  -testResults "D:/biformis_android/Logs/tests.xml" \
  -logFile "D:/biformis_android/Logs/tests.log"
```

| Test | Bảo vệ điều gì |
|---|---|
| `BuildsTheExpectedControls` | HUD dựng đủ joystick + 3 nút, gamepad ảo được tạo |
| `JoystickDrivesTheMoveAction` | Kéo phải → `Move.x > 0.5`; thả → về 0 |
| `JoystickKeepsSteeringThroughAnInterruptionWithoutLiftingTheFinger` | Bug §6 không tái phát |
| `RunButtonHoldsRunAndTapsDash` | Một nút cho cả `Run` lẫn `Dash`, đúng như `LeftShift` |
| `DimensionButtonFiresTheSameActionAsTheJKey` | Nút bắn đúng action mà `J` bắn |
| `PauseButtonFiresTheSameActionAsEscape` | Nút bắn đúng `UI/Cancel` |
| `HitStopNeverRestoresAFrozenClock` | Bug §5.1 nguyên nhân 1 |
| `AnAbortedHitStopNeverWritesItsSavedClockLater` | Bug §5.1 nguyên nhân 2 |
| `AHitStopStillRestoresANormalClock` | Bản sửa không phá hit-stop bình thường |

Thư mục `Tests/` có `defineConstraints: UNITY_INCLUDE_TESTS` nên **không vào APK**. Xóa được nếu không muốn giữ.

---

## 11. Việc còn tồn đọng

| Việc | Trạng thái |
|---|---|
| **Multi-touch** (giữ sprint + đẩy joystick cùng lúc) | ❌ Chưa kiểm chứng — phải thử tay trên máy |
| **Shockwave sau khi sửa bound = 8** | ❌ Chưa nhìn bằng mắt |
| **FPS sau khi bật Camera Sorting Layer Texture** | ❌ Chưa đo — thêm 1 lần copy full-screen mỗi frame |
| Nút Interact | Đã code, tắt mặc định (§6) |
| Package name | Vẫn là mặc định |
| Tablet / màn hẹp hơn 16:9 | Chạy được nhưng thấy hẹp hơn PC (§8) |
| Rung phản hồi (haptic) | Chưa làm — NiceVibrations đã có sẵn trong project |

---

## 12. Nếu phải làm lại bản Android từ đầu

Thứ tự đã chứng minh là hiệu quả:

1. **Đọc kiến trúc input trước.** Câu hỏi quyết định: game dùng chung một `PlayerInput` hay mỗi hệ thống tự `new`? Ở đây là vế sau → buộc phải giả lập ở tầng device.
2. **Lập bảng ánh xạ phím → control path** trước khi viết dòng code nào. Ưu tiên path đã được bind sẵn.
3. **Kiểm chứng bằng grep GUID của prefab, không phải của script** khi muốn biết thứ gì có trong scene.
4. **Dựng HUD bằng code**, không prefab — giữ scene sạch, diff nhỏ, không đụng thứ tự scene.
5. **Xử lý khung hình sớm.** Camera orthographic + màn không phải 16:9 là vấn đề chắc chắn gặp, không phải "nếu".
6. **Ngờ vực mọi chỗ ghi `Time.timeScale`.** Đây là nguồn bug nặng nhất và khó tái hiện nhất.
7. **Ngờ vực mọi so sánh float tuyệt đối với input.** Bàn phím cho giá trị rời rạc, joystick thì không.
8. **Ngờ vực mọi thứ chạy theo sự kiện "input đổi giá trị".** Bàn phím bắn vài sự kiện, joystick bắn mỗi frame. Cái gì tốn kém hoặc có tác dụng phụ (phát animation, tạo timer, spawn) mà nằm trong đường đó đều sẽ nổ.
8. **Ngờ vực mọi shader lấy mẫu texture của render pipeline.** DX11 tha thứ, Vulkan/GLES thì không.
9. **Viết test cho từng thứ sửa được headless.** Nhưng đừng cố bơm Touchscreen ảo trong batchmode — không chạy.
