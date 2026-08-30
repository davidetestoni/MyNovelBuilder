import { Component } from '@angular/core';
import { RouterModule } from '@angular/router';
import { appReleaseLabel, appVersion } from '../../../app-metadata';

@Component({
  selector: 'app-main-layout-header',
  standalone: true,
  imports: [RouterModule],
  templateUrl: './main-layout-header.component.html',
  styleUrl: './main-layout-header.component.scss',
})
export class MainLayoutHeaderComponent {
  readonly version = appVersion;
  readonly releaseLabel = appReleaseLabel;
}
