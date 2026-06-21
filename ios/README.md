# iOS — Swift + SwiftUI

This directory contains the native iOS application for MAI Health Coach.

## Stack

| Concern       | Technology                              |
|---------------|-----------------------------------------|
| Language      | Swift 5.10+                             |
| UI Framework  | SwiftUI                                 |
| Auth          | Clerk iOS SDK                           |
| Networking    | URLSession / async-await                |
| Barcode Scan  | AVFoundation (native camera)            |
| Testing       | XCTest                                  |
| Package Mgr   | Swift Package Manager                   |

## Planned Structure

```
ios/
├── MAIHealthCoach/
│   ├── App/               # App entry, configuration
│   ├── Features/          # Feature modules (Auth, Log, Coach, Scan, …)
│   ├── Services/          # API client, keychain, etc.
│   ├── Models/            # Swift data models / Codable types
│   └── Resources/         # Assets, localizations
├── MAIHealthCoachTests/
├── MAIHealthCoach.xcodeproj/
└── Config/
    ├── Debug.xcconfig      # API_BASE_URL, CLERK_PUBLISHABLE_KEY
    └── Release.xcconfig
```

## Coming in Later Tickets

- M1: Xcode project scaffold, Clerk auth, root navigation
- M2: Daily log screens (food, water, exercise)
- M3: Barcode scanner screen (AVFoundation)
- M4: AI coach screen (nudges + suggestions)

## Local Run

See the root [README.md](../README.md#ios-swiftswiftui). Requires macOS + Xcode 16+.
