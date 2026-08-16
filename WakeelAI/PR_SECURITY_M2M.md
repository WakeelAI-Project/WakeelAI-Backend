# PR: security(m2m): Production Security Hardening & Secret Management

## 1. PR Title & Executive Summary
**Title:** `security(m2m): implement constant-time key comparison, secure appsettings, and automated HttpClient header injection`

**Summary:** 
This Pull Request secures the .NET 10 backend for immediate production deployment. It targets critical security vulnerabilities identified during our DevSecOps review. Specifically, it eliminates hardcoded secrets from configuration files, establishes a cryptographically secure method for verifying Machine-to-Machine (M2M) API requests, and seamlessly automates the injection of the required secure headers into our Node.js AI service integrations. 

## 2. Security & DevSecOps Hardening
- **Timing Attack Prevention (Constant-Time Comparison):** 
  - *Vulnerability:* The previous `InternalApiKeyMiddleware` compared incoming API keys using standard string equality (`==`). This operation short-circuits on the first mismatched character, allowing attackers to progressively guess the API key by measuring response times (Timing Attack).
  - *Resolution:* Completely refactored the middleware to utilize `System.Security.Cryptography.CryptographicOperations.FixedTimeEquals()`. This guarantees the comparison takes the exact same amount of time regardless of where the mismatch occurs, completely mitigating the timing attack vector.

- **Configuration Overhaul & Secret Management:** 
  - *Vulnerability:* `appsettings.json` previously contained exposed, hardcoded secrets including the Database Connection String, JWT Secret Key, SMTP Password, and the `AiNode:InternalApiKey`.
  - *Resolution:* Safely updated `appsettings.json`. The application is now strictly structured to pull sensitive overrides from the production Environment Variables or GitHub Actions Secrets (`${{ secrets.WAKEEL_INTERNAL_API_KEY }}`) at runtime on MonsterASP.

## 3. Architectural Improvements
- **M2M HttpClient Auto-Injection:** 
  - *Refactoring:* Updated the typed `AiNodeClient` inside `InfrastructureServiceCollectionExtensions.cs`. 
  - *Behavior:* The `HttpClient` now automatically retrieves the secure `X-Internal-API-Key` from the configuration environment and attaches it as a `DefaultRequestHeader`. This centralized approach ensures that all outbound requests to the Node.js production service (`https://wakeelai-ai.onrender.com`) are authenticated seamlessly without repetitive header additions in individual controllers.

## 4. Verification & Testing
- The new 64-character hexadecimal security key (`08963d8cc843324bcd2d2e13342b96df13c9512b6fff960e4d2e4e34ec806b54`) is properly integrated.
- Verified that missing or invalid API keys strictly trigger a `401 Unauthorized` response with a flat JSON envelope (`{ "error": "unauthorized", "message": "..." }`) before reaching any JWT logic.
