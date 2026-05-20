# Analysis Document: Connecting a React App with AWS Cognito Using Microsoft Email SSO

## 1. Objective

The objective is to allow users with Microsoft work or school email accounts to sign in to a React application using Single Sign-On through **AWS Cognito**.

The recommended enterprise setup is:

```text
React App
→ AWS Cognito Hosted UI
→ Microsoft Entra ID
→ AWS Cognito User Pool
→ Cognito JWT Tokens
→ Backend API
```

In this model, the React app does not authenticate directly with Microsoft. Instead, **AWS Cognito acts as the authentication broker**. Microsoft Entra ID verifies the user, and Cognito issues the final tokens that the React app and backend will trust.

---

## 2. Recommended Integration Approach

There are two common ways to connect Microsoft login with AWS Cognito:

| Option       | Description                                                                                    | Recommendation                                 |
| ------------ | ---------------------------------------------------------------------------------------------- | ---------------------------------------------- |
| **SAML 2.0** | Microsoft Entra ID acts as a SAML Identity Provider, and Cognito acts as the Service Provider. | Best for enterprise/corporate SSO.             |
| **OIDC**     | Microsoft Entra ID acts as an OpenID Connect provider.                                         | Good for modern OAuth/OIDC-based integrations. |

For enterprise Microsoft email login, especially where the company uses Azure AD / Microsoft Entra ID, **SAML 2.0 is a strong choice**.

AWS Cognito supports external SAML identity providers for user pools and provides specific values for the Service Provider Entity ID and Assertion Consumer Service URL. Microsoft Entra ID also supports SAML-based enterprise application SSO. ([AWS Documentation][1])

---

## 3. Target Architecture

```text
User
  ↓
React Application
  ↓
Cognito Hosted UI
  ↓
Microsoft Entra ID SAML Login
  ↓
SAML Assertion returned to Cognito
  ↓
Cognito User Pool validates assertion
  ↓
Cognito issues ID token and access token
  ↓
React app receives Cognito session
  ↓
React app calls backend APIs
  ↓
API Gateway / Backend validates Cognito JWT
```

### Key Point

The React application should **not process SAML directly**. SAML exchange should happen between Microsoft Entra ID and AWS Cognito. React should only interact with Cognito using the Hosted UI and Cognito-issued tokens.

---

## 4. Main Components Required

### Frontend

The React app will use Cognito Hosted UI for sign-in and sign-out.

Required frontend pieces:

```text
React application
Cognito Hosted UI redirect
Callback route in React
Session/token handling
Protected routes
API calls with Cognito access token
```

### AWS Components

```text
Amazon Cognito User Pool
Cognito App Client
Cognito Hosted UI
Cognito Domain
SAML Identity Provider configuration
Attribute mapping
Optional: API Gateway JWT Authorizer
Optional: Lambda triggers for domain restrictions
```

### Microsoft Components

```text
Microsoft Entra ID tenant
Enterprise Application
SAML Single Sign-On configuration
User/group assignment
SAML claims
Federation Metadata XML
```

---

## 5. Authentication Flow Explanation

### Step 1: User Opens React App

The user opens the React application and clicks:

```text
Continue with Microsoft
```

### Step 2: React Redirects to Cognito

The React app redirects the user to the Cognito Hosted UI.

The Hosted UI can be configured to show only Microsoft SSO, or it can show multiple login options.

### Step 3: Cognito Redirects to Microsoft Entra ID

Cognito recognizes Microsoft Entra ID as a configured SAML identity provider and redirects the user to Microsoft’s login page.

### Step 4: Microsoft Authenticates the User

Microsoft Entra ID authenticates the user using the organization’s policies.

This may include:

```text
Password authentication
Multi-factor authentication
Conditional Access
Device compliance checks
Corporate network restrictions
```

### Step 5: Microsoft Sends SAML Assertion to Cognito

After successful login, Microsoft sends a SAML assertion to Cognito’s SAML endpoint.

The Cognito Assertion Consumer Service URL follows this format:

```text
https://<cognito-domain>/saml2/idpresponse
```

AWS documents this as the SAML response endpoint for Cognito user pools. ([AWS Documentation][1])

### Step 6: Cognito Validates the Assertion

Cognito validates:

```text
SAML signature
Issuer
Audience
Attributes
User mapping
Metadata certificate
```

If valid, Cognito creates or locates the federated user in the User Pool.

### Step 7: Cognito Issues Tokens

Cognito then issues:

```text
ID token
Access token
Refresh token, if configured
```

The React app uses these Cognito tokens, not Microsoft tokens.

### Step 8: React Calls Backend APIs

The React app sends the Cognito access token to backend services.

### Step 9: Backend Validates Cognito Token

The backend should validate the token either through:

```text
API Gateway JWT Authorizer
Backend JWT validation
Lambda authorizer
```

For AWS-native architecture, API Gateway JWT Authorizer is usually the cleanest option.

---

## 6. AWS Cognito Configuration Steps

### 6.1 Create or Open Cognito User Pool

Create a Cognito User Pool or use an existing one.

Recommended configuration:

```text
User Pool name: Application-specific
Sign-in options: Federated SSO
Required attributes: email, name
MFA: Optional or required depending on business need
Account recovery: Email-based, if local users are also enabled
```

If the app will only use Microsoft SSO, local username/password login can be disabled or hidden from the Hosted UI.

---

### 6.2 Create Cognito App Client

Create an app client for the React application.

Recommended app client settings:

```text
App type: Single-page application
Client secret: Disabled
OAuth flow: Authorization Code Grant
PKCE: Enabled
Allowed scopes: openid, email, profile
Callback URL: React app URL
Sign-out URL: React app URL
```

For a React single-page app, the client secret must be disabled because frontend code is visible to users.

AWS recommends authorization code grant for apps that need tokens, and Cognito supports PKCE for public clients such as browser-based apps. ([AWS Documentation][2])

---

### 6.3 Configure Cognito Domain

A Cognito domain is required for Hosted UI and SAML federation.

You can use either:

```text
Cognito default domain
Custom domain
```

Example default Cognito domain:

```text
https://myapp.auth.ap-south-1.amazoncognito.com
```

For production, a custom domain is better:

```text
https://auth.yourcompany.com
```

---

### 6.4 Add Microsoft Entra ID as a SAML Provider

In Cognito, add a new SAML identity provider.

Recommended provider name:

```text
MicrosoftSAML
```

The provider name should avoid spaces because it will be referenced in login URLs and frontend configuration.

Cognito needs Microsoft’s metadata XML. This metadata contains the Microsoft SAML signing certificate, entity ID, and SSO service endpoint. AWS Cognito supports adding a SAML provider using metadata document or metadata URL. ([AWS Documentation][3])

---

### 6.5 Configure Cognito SAML Attribute Mapping

Map Microsoft SAML claims to Cognito user attributes.

Recommended mapping:

| Cognito Attribute    | Microsoft SAML Claim                 |
| -------------------- | ------------------------------------ |
| `email`              | email address or user principal name |
| `name`               | display name                         |
| `given_name`         | first name                           |
| `family_name`        | surname                              |
| `preferred_username` | user principal name                  |

Important: in Microsoft Entra ID, `mail` may sometimes be empty. For many organizations, `userPrincipalName` is more reliable.

Recommended rule:

```text
Use userPrincipalName as fallback for email if mail is not always available.
```

If Cognito requires email, Microsoft must always send a valid value for the mapped email attribute.

---

### 6.6 Enable Microsoft Provider for the App Client

After adding the SAML provider, enable it for the Cognito app client.

Hosted UI settings should include:

```text
Identity provider: MicrosoftSAML
OAuth grant type: Authorization Code Grant
Scopes: openid, email, profile
Callback URLs: React app callback URLs
Sign-out URLs: React app logout URLs
```

For production, include only trusted HTTPS URLs.

---

## 7. Microsoft Entra ID Configuration Steps

### 7.1 Create Enterprise Application

In Microsoft Entra admin center:

```text
Enterprise applications
→ New application
→ Create your own application
→ Integrate any other application
```

Name example:

```text
React Cognito SSO
```

---

### 7.2 Enable SAML Single Sign-On

Inside the Enterprise Application:

```text
Single sign-on
→ SAML
```

Microsoft’s SAML SSO setup requires configuring the Basic SAML Configuration, including the reply URL and sign-on URL. ([Microsoft Learn][4])

---

### 7.3 Configure Basic SAML Settings

Use Cognito values.

#### Identifier / Entity ID

```text
urn:amazon:cognito:sp:<user-pool-id>
```

Example:

```text
urn:amazon:cognito:sp:ap-south-1_AbCdEf123
```

#### Reply URL / ACS URL

```text
https://<cognito-domain>/saml2/idpresponse
```

Example:

```text
https://myapp.auth.ap-south-1.amazoncognito.com/saml2/idpresponse
```

#### Sign-on URL

Use the Cognito Hosted UI authorize URL.

Conceptually, it points to:

```text
Cognito Hosted UI authorize endpoint
+ MicrosoftSAML identity provider
+ response type code
+ Cognito app client ID
+ React redirect URL
+ required scopes
```

---

### 7.4 Configure Microsoft Claims

Recommended claims:

| Claim                | Source                                  |
| -------------------- | --------------------------------------- |
| `email`              | `user.mail` or `user.userprincipalname` |
| `name`               | `user.displayname`                      |
| `given_name`         | `user.givenname`                        |
| `family_name`        | `user.surname`                          |
| `preferred_username` | `user.userprincipalname`                |

For many enterprise tenants, use:

```text
email = user.userprincipalname
preferred_username = user.userprincipalname
```

This avoids login failures when `user.mail` is empty.

---

### 7.5 Assign Users or Groups

In the Microsoft Enterprise Application:

```text
Users and groups
→ Add user/group
```

Assign only the users or groups who should access the React application.

This is the preferred access-control point because unauthorized users will be blocked before reaching Cognito.

---

### 7.6 Download Federation Metadata XML

From the Microsoft SAML configuration page, download:

```text
Federation Metadata XML
```

This file must be uploaded into Cognito when adding the SAML identity provider.

---

## 8. React Application Integration Steps

The React app should not contain Microsoft-specific authentication logic.

It should only know:

```text
Cognito domain
Cognito user pool ID
Cognito app client ID
Redirect sign-in URL
Redirect sign-out URL
Provider name
```

### React Login Behavior

The React app should provide a button such as:

```text
Continue with Microsoft
```

When clicked, it redirects the user to Cognito Hosted UI and specifies Microsoft as the identity provider.

### React Callback Handling

After successful authentication, Cognito redirects the user back to the configured React callback URL.

The React app should then:

```text
Detect successful login
Store Cognito session through the auth library
Read user profile from ID token
Use access token for API calls
Redirect user to the protected page
```

### React Logout Behavior

Logout should clear the local app session and redirect to Cognito logout.

For complete logout, consider whether the user should also be logged out of Microsoft. Many enterprise SSO flows intentionally keep the Microsoft session active, so a user may be silently signed back in during the next login attempt.

---

## 9. Backend API Security

The backend must never trust the frontend alone.

Even if React hides protected pages, every backend API must validate the Cognito access token.

Recommended AWS-native setup:

```text
React App
→ API Gateway
→ JWT Authorizer
→ Backend API / Lambda / Orchestrator
```

API Gateway JWT Authorizer should validate:

```text
Issuer: Cognito User Pool issuer
Audience: Cognito App Client ID
Token signature
Token expiry
Scopes or claims
```

This protects backend services even if someone bypasses the React UI.

---

## 10. User Identity and Authorization

Authentication confirms who the user is. Authorization decides what the user can do.

After login, authorization can be based on:

```text
Microsoft group assignment
Cognito groups
Custom attributes
Email domain
Backend role mapping
Application database roles
```

Recommended enterprise approach:

```text
Microsoft Entra group assignment controls who can log in.
Application backend controls what each user can access.
```

For example:

| Layer              | Responsibility                                   |
| ------------------ | ------------------------------------------------ |
| Microsoft Entra ID | User authentication and enterprise access policy |
| Cognito            | Federation, token issuance, user pool identity   |
| API Gateway        | Token validation                                 |
| Backend API        | Business-level authorization                     |
| App database       | Application roles and permissions                |

---

## 11. Restricting Login to Microsoft Email Users

There are multiple ways to restrict access.

### Option 1: Restrict by Microsoft Tenant

Use a single Microsoft Entra tenant and assign only users from that tenant.

Best for corporate apps.

### Option 2: Restrict by Assigned Users or Groups

In Microsoft Entra ID, only assign allowed users or groups to the Enterprise Application.

This is usually the best operational control.

### Option 3: Restrict by Email Domain

If only users from a specific domain should log in, enforce domain checks in one or more places:

```text
Microsoft Entra ID
Cognito Lambda trigger
Backend API authorization layer
```

Example allowed domain:

```text
@company.com
```

Recommended practice:

```text
Use Microsoft group assignment as primary control.
Use backend authorization as final enforcement.
```

---

## 12. Security Considerations

### Use Authorization Code Flow with PKCE

React is a public client. It cannot safely store secrets.

Therefore:

```text
Use Authorization Code Flow
Enable PKCE
Disable client secret
Avoid implicit flow
```

AWS Cognito supports PKCE with authorization code grant to protect public clients. ([AWS Documentation][2])

### Use HTTPS

Production callback and sign-out URLs should always use HTTPS.

Avoid allowing broad callback URLs.

### Avoid Token Storage in Local Storage

Token storage should be handled carefully. Prefer trusted authentication libraries and secure session handling patterns.

### Validate Tokens on Backend

Do not rely only on frontend route protection.

### Rotate SAML Certificates

Microsoft SAML signing certificates can expire or rotate. Cognito metadata must stay updated.

If possible, use metadata URL instead of static metadata upload to simplify certificate updates.

### Enable MFA in Microsoft Entra ID

For enterprise apps, enforce MFA through Microsoft Entra Conditional Access rather than building MFA into the React app.

---

## 13. Testing Plan

### Test 1: Basic SSO Login

Verify:

```text
User clicks Continue with Microsoft
User is redirected to Microsoft login
User authenticates successfully
User returns to React app
React app receives Cognito session
```

### Test 2: Unauthorized Microsoft User

Verify that an unassigned Microsoft user cannot log in.

Expected result:

```text
Microsoft blocks access before Cognito login completes.
```

### Test 3: Missing Attribute

Test a user where `mail` is empty.

If login fails, map Cognito email to `userPrincipalName` instead.

### Test 4: Backend Token Validation

Call backend API with:

```text
Valid Cognito token
Expired token
Missing token
Invalid token
```

Expected result:

```text
Only valid Cognito tokens should be accepted.
```

### Test 5: Logout Behavior

Verify:

```text
React session clears
Cognito session clears
Next login behavior is acceptable
```

Remember: Microsoft session may remain active unless explicitly logged out.

---

## 14. Common Issues and Fixes

| Issue                            | Likely Cause                     | Fix                                                         |
| -------------------------------- | -------------------------------- | ----------------------------------------------------------- |
| Login redirects fail             | Incorrect callback URL           | Match React callback URL in Cognito exactly                 |
| SAML response rejected           | Wrong Entity ID or ACS URL       | Use Cognito-provided values                                 |
| User created without email       | Incorrect attribute mapping      | Map email to `userPrincipalName`                            |
| User cannot access app           | Not assigned in Microsoft Entra  | Assign user or group                                        |
| React gets no token              | Incorrect OAuth flow or callback | Use authorization code grant with PKCE                      |
| Backend rejects token            | Wrong issuer/audience            | Configure API Gateway with Cognito issuer and app client ID |
| Works locally but not production | Missing production callback URL  | Add HTTPS production URL to Cognito                         |

---

## 15. Recommended Final Configuration

### Microsoft Entra ID

```text
Application type: Enterprise Application
SSO method: SAML
Identifier: urn:amazon:cognito:sp:<user-pool-id>
Reply URL: https://<cognito-domain>/saml2/idpresponse
Claims: email, name, given_name, family_name, preferred_username
Access control: Assigned users/groups only
MFA/Conditional Access: Managed in Microsoft Entra ID
```

### AWS Cognito

```text
User Pool: Enabled
App Client: SPA client without secret
OAuth flow: Authorization Code Grant
PKCE: Enabled
Scopes: openid, email, profile
Identity Provider: MicrosoftSAML
Hosted UI: Enabled
Callback URL: React app URL
Sign-out URL: React app URL
```

### React App

```text
Uses Cognito Hosted UI
Redirects login to Cognito
Receives Cognito session after login
Uses Cognito access token for backend APIs
Does not directly process SAML
Does not store client secrets
```

### Backend

```text
Validates Cognito JWT
Uses API Gateway JWT Authorizer or backend JWT validation
Applies business authorization
Logs authentication and authorization failures
```

---

## 16. Final Recommended Flow for Your React Chatbot UI

```text
React Chatbot UI
→ Cognito Hosted UI
→ Microsoft Entra ID SAML SSO
→ Cognito User Pool
→ Cognito JWT
→ API Gateway JWT Authorizer
→ XAPI Pass-through Layer
→ Orchestrator API
→ AWS Bedrock / External APIs
```
[3]: https://docs.aws.amazon.com/cognito/latest/developerguide/cognito-user-pools-integrating-3rd-party-saml-providers.html?utm_source=chatgpt.com "Configuring your third-party SAML identity provider"
[4]: https://learn.microsoft.com/en-us/entra/identity/enterprise-apps/add-application-portal-setup-sso?utm_source=chatgpt.com "Enable SAML single sign-on for an enterprise application"
