import { Component } from '@angular/core';
import { appReleaseLabel, appVersion } from '../../app-metadata';

@Component({
  selector: 'app-about',
  standalone: true,
  templateUrl: './about.component.html',
  styleUrl: './about.component.scss',
})
export class AboutComponent {
  readonly version = appVersion;
  readonly releaseLabel = appReleaseLabel;
}
