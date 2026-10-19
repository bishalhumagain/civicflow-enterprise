# Local Keycloak realm fixture

The adjacent `civicflow-realm.json` is a disposable local realm definition. It contains public client configuration only; it contains no users, passwords, client secrets, or production settings.

The Compose service imports this realm on first initialization. Create test users through the Keycloak admin UI or an external setup script using local-only credentials. Do not commit exported users or credentials.
