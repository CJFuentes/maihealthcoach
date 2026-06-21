# Android — Kotlin + Jetpack Compose

This directory contains the native Android application for MAI Health Coach.

## Stack

| Concern       | Technology                              |
|---------------|-----------------------------------------|
| Language      | Kotlin                                  |
| UI Framework  | Jetpack Compose                         |
| Auth          | Clerk Android SDK                       |
| Networking    | Retrofit + OkHttp + Kotlin Coroutines   |
| Barcode Scan  | CameraX + ML Kit Barcode Scanning       |
| Testing       | JUnit4, Robolectric, Espresso           |
| Build         | Gradle (Kotlin DSL)                     |

## Planned Structure

```
android/
├── app/
│   └── src/
│       ├── main/
│       │   ├── java/com/maihealthcoach/
│       │   │   ├── di/            # Dependency injection (Hilt)
│       │   │   ├── features/      # Feature modules
│       │   │   ├── data/          # Repositories, API, local DB
│       │   │   ├── domain/        # Use cases, models
│       │   │   └── ui/            # Compose screens, ViewModels
│       │   └── res/
│       └── test/
├── build.gradle.kts
└── settings.gradle.kts
```

## Coming in Later Tickets

- M1: Gradle project scaffold, Clerk auth, navigation skeleton
- M2: Daily log screens (Compose)
- M3: Barcode scanner (CameraX + ML Kit)
- M4: AI coach screen

## Local Run

See the root [README.md](../README.md#android-kotlincompose). Open `android/` in Android Studio.
