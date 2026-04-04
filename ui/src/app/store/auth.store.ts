import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withHooks, withMethods, withProps, withState } from '@ngrx/signals';
import { lastValueFrom } from 'rxjs';
import { AuthService, type TokenResponse } from '@app/services/auth-service';

export type { TokenResponse };

export interface AuthState {
  user: TokenResponse | null;
  isLoading: boolean;
  loginError: string | null;
  registerSuccess: boolean;
}

const initialState: AuthState = {
  user: null,
  isLoading: false,
  loginError: null,
  registerSuccess: false,
};

type Credentials = { username: string; password: string };
type RegisterRequest = Credentials & { email: string };

export const AuthStore = signalStore(
  { providedIn: 'root' },

  withState(initialState),

  withProps(() => ({
    _authService: inject(AuthService),
  })),

  withComputed(({ user }) => ({
    isLoggedIn: computed(() => {
      const u = user();
      return u !== null && new Date(u.expiresAt) > new Date();
    }),
    displayName: computed(() => user()?.username ?? null),
  })),

  withMethods((store) => ({
    async login(credentials: Credentials): Promise<void> {
      patchState(store, { isLoading: true, loginError: null });
      try {
        const user = await lastValueFrom(store._authService.login(credentials));
        patchState(store, { user, isLoading: false });
      } catch (err: unknown) {
        const message = err instanceof Error ? err.message : 'Login failed. Please try again.';
        patchState(store, { isLoading: false, loginError: message });
      }
    },

    async register(req: RegisterRequest): Promise<void> {
      patchState(store, { isLoading: true });
      try {
        await lastValueFrom(store._authService.register(req));
        patchState(store, { isLoading: false, registerSuccess: true });
      } catch (err: unknown) {
        const message = err instanceof Error ? err.message : 'Registration failed. Please try again.';
        patchState(store, { isLoading: false, loginError: message });
      }
    },

    logout(): void {
      patchState(store, initialState);
    },

    clearLoginError(): void {
      patchState(store, { loginError: null });
    },

    clearRegisterSuccess(): void {
      patchState(store, { registerSuccess: false });
    },

    setUserFromExternal(user: TokenResponse): void {
      patchState(store, { user });
    },
  })),

  withHooks((store) => ({
    onInit() {
      const stored = localStorage.getItem('loginSession');
      if (!stored) return;
      try {
        const user = JSON.parse(stored) as TokenResponse;
        if (new Date(user.expiresAt) > new Date()) {
          patchState(store, { user });
        } else {
          localStorage.removeItem('loginSession');
        }
      } catch {
        localStorage.removeItem('loginSession');
      }
    },
  })),
);
