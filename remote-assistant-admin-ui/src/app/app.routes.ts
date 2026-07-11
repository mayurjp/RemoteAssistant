import { Routes } from '@angular/router';
import { DashboardComponent } from './components/dashboard/dashboard.component';
import { SetupComponent } from './components/setup/setup.component';
import { OAuthCallbackComponent } from './components/oauth-callback/oauth-callback.component';

export const routes: Routes = [
  { path: 'dashboard', component: DashboardComponent },
  { path: 'setup', component: SetupComponent },
  { path: 'oauth-callback', component: OAuthCallbackComponent },
  { path: '', redirectTo: '/dashboard', pathMatch: 'full' },
  { path: '**', redirectTo: '/dashboard' }
];
