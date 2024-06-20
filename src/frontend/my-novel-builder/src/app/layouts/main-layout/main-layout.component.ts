import { Component } from '@angular/core';
import { MainLayoutHeaderComponent } from './main-layout-header/main-layout-header.component';

@Component({
  selector: 'app-main-layout',
  standalone: true,
  templateUrl: './main-layout.component.html',
  styleUrl: './main-layout.component.scss',
  imports: [MainLayoutHeaderComponent],
})
export class MainLayoutComponent {}
