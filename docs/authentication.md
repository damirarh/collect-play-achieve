# User authentication

## Rules

- Uses ASP.NET Identity without default `MapIdentity` endpoints.
- No e-mail confirmation or password reset flows; users are created and managed by the API.
- Only the first user registers through a register endpoint, which is disabled after the first user is created. The first user is automatically assigned the `Administrator` role.
- If the administrator(s) can't log in, the password can be reset via command line on the host machine.
- The login endpoint uses the cookie approach.

## Endpoints

- `GET /init/status` - returns the registration status of the first user.
- `POST /init/register` - registers the first user with a username and password.
- `POST /auth/login` - logs in a user with a username and password.
- `POST /auth/logout` - logs out the current user.
- `PUT /auth/password` - changes the current logged-in user's password.
- `GET /auth/me` - returns the current logged-in user's information.
- `PUT /auth/me` - updates the current logged-in user's information.
- `GET /auth/users` - returns a list of all users (requires `Administrator` role).
- `POST /auth/users` - creates a new user (requires `Administrator` role).
- `GET /auth/users/{id}` - returns a specific user's information (requires `Administrator` role).
- `PUT /auth/users/{id}` - updates a specific user's information (requires `Administrator` role).
- `DELETE /auth/users/{id}` - deletes a specific user (requires `Administrator` role).

## CLI commands

- `CollectPlayAchieve.Api user list` - lists all users.
- `CollectPlayAchieve.Api user reset-password -u <username> -p <new-password>` - resets the password for a specific user.
