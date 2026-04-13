import { Component, effect, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthStore } from '@app/store/auth.store';
import { environment } from '@env/environment';
import { email, form, FormField, pattern, required, validate } from '@angular/forms/signals';

type LoginData = {
  username: string;
  password: string;
};

type RegisterData = LoginData & {
  email: string;
  confirmPassword: string;
};

@Component({
  selector: 'App-Login',
  templateUrl: './loginOrRegister.component.html',
  styleUrl: './loginOrRegister.component.css',
  imports: [FormField],
})
export class LoginOrRegisterComponent {
  readonly authStore = inject(AuthStore);
  private readonly router = inject(Router);

  readonly loginModel = signal<LoginData>({ username: '', password: '' });

  readonly registerModel = signal<RegisterData>({
    username: '',
    password: '',
    email: '',
    confirmPassword: '',
  });

  readonly loginForm = form(this.loginModel, (p) => {
    required(p.username);
    required(p.password);
  });

  readonly registerForm = form(this.registerModel, (p) => {
    required(p.username);
    required(p.password);
    required(p.email);
    required(p.confirmPassword);
    pattern(p.password, /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z\d]).{8,}$/, {
      message: 'Password must be at least 8 characters and include uppercase, lowercase, digit, and special character',
    });
    email(p.email, { message: 'Please enter a valid email address' });
    validate(p.confirmPassword, ({ value, valueOf }) => {
      if (value() !== valueOf(p.password)) {
        return { kind: 'passwordMismatch', message: 'Passwords do not match' };
      }
      return null;
    });
  });

  constructor() {
    // Navigate away when login succeeds (tracks the user() signal synchronously).
    effect(() => {
      if (this.authStore.user() && !this.authStore.isExpired()) {
        this.router.navigate(['/']);
      }
    });

    // Side effect: reset register form after successful registration.
    effect(() => {
      if (this.authStore.registerSuccess()) {
        this.registerModel.set({ username: '', password: '', email: '', confirmPassword: '' });
        this.authStore.clearRegisterSuccess();
      }
    });
  }

  loginWithGoogle() {
    window.location.href = `${environment.webApiBaseUrl}/api/auth/external/challenge`;
  }

  onLoginSubmit() {
    if (this.loginForm().invalid()) {
      this.loginForm()
        .errorSummary()
        .forEach((error) => error.fieldTree().markAsTouched());
      return;
    }
    this.authStore.login({
      username: this.loginModel().username,
      password: this.loginModel().password,
    });
  }

  onRegisterSubmit() {
    if (this.registerForm().invalid()) {
      this.registerForm()
        .errorSummary()
        .forEach((error) => error.fieldTree().markAsTouched());
      return;
    }
    this.authStore.register({
      username: this.registerModel().username,
      password: this.registerModel().password,
      email: this.registerModel().email,
    });
  }
}
