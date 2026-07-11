import { Routes } from '@angular/router';
import { SetupComponent } from './components/setup/setup.component';
import { LoginComponent } from './components/login/login.component';
import { AuthGuard } from './services/auth.guard';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'bots', component: SetupComponent, canActivate: [AuthGuard] },
  { path: '', redirectTo: '/bots', pathMatch: 'full' },
  { path: '**', redirectTo: '/bots' }
];
