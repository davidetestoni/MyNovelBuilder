import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { EmptyLayoutComponent } from './layouts/empty-layout/empty-layout.component';
import { MainLayoutComponent } from './layouts/main-layout/main-layout.component';
import { LayoutType } from './types/enums/layout-type';
import { LayoutService } from './services/layout.service';

@Component({
  selector: 'app-root',
  standalone: true,
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
  imports: [RouterOutlet, EmptyLayoutComponent, MainLayoutComponent],
})
export class AppComponent {
  private readonly layoutService = inject(LayoutService);

  LayoutType = LayoutType;
  readonly layoutType = this.layoutService.layoutType;
}
