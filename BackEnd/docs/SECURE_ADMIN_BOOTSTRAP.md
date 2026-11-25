# Secure administrator bootstrap

The API no longer creates an administrator from a built-in username or password.
Normal permission and role initialization still runs during startup.

To create the first administrator, provide the following values through a secret
configuration provider for a controlled bootstrap operation:

```text
Security__BootstrapAdmin__Enabled=true
Security__BootstrapAdmin__UserName=<administrator username>
Security__BootstrapAdmin__Email=<administrator email>
Security__BootstrapAdmin__DisplayName=<administrator display name>
Security__BootstrapAdmin__Password=<strong one-time password, at least 14 characters>
Security__BootstrapAdmin__AllowInProduction=true
```

The production override must be enabled explicitly. The bootstrap is one-time:
if the username or email already exists, it does nothing and never changes the
existing password or profile. The administrator is created with
`MustChangePassword=true` so the first login must rotate the bootstrap password.
The issued JWT carries that state, and the API blocks normal authenticated
endpoints until the password-change endpoint completes successfully. The
account information endpoint and password-change endpoint remain available so
the client can guide the user through the required rotation.

## First-login contract

1. The user signs in with the one-time bootstrap password and receives a JWT.
2. The API permits only account-state and password-change operations while
   `MustChangePassword` is true. The database value is checked on every
   authenticated request; the JWT claim is only an additional restriction.
3. The client submits the current and new passwords to
   `POST /api/account/change-password`.
4. After success, the client must discard the old JWT and sign in again. A
   newly issued JWT then permits normal authenticated routes.

The backend intentionally does not change the frontend in this PR. A separate
frontend task must detect `PASSWORD_CHANGE_REQUIRED`, route the user to the
password-change screen, clear the old token after success, and reauthenticate.
The anonymous login and password-reset endpoints remain reachable even if a
stale bearer token is attached to the request.

After the first successful bootstrap, remove the bootstrap values from the
runtime secret store and disable the feature. Never commit these values or put
them in tracked application settings.
