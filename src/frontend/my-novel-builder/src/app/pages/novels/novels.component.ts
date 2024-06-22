import { Component, Inject, OnInit, inject } from '@angular/core';
import { NovelService } from '../../services/novel.service';
import { NovelDto } from '../../types/dtos/novel/novel.dto';
import { EllipsisPipe } from '../../pipes/ellipsis.pipe';
import moment from 'moment';
import { RouterModule } from '@angular/router';
import { Observable } from 'rxjs';
import { AsyncPipe } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { CreateNovelComponent } from '../../components/create-novel/create-novel.component';
import { MatDialog } from '@angular/material/dialog';

@Component({
  selector: 'app-novels',
  standalone: true,
  templateUrl: './novels.component.html',
  styleUrls: ['./novels.component.scss'],
  imports: [EllipsisPipe, RouterModule, AsyncPipe, ReactiveFormsModule],
})
export class NovelsComponent implements OnInit {
  novels: NovelDto[] | null = null;
  readonly dialog = inject(MatDialog);
  readonly novelService = inject(NovelService);

  ngOnInit(): void {
    this.getNovels();
  }

  getNovels(): void {
    this.novelService.getNovels().subscribe((novels) => {
      this.novels = novels;
    });
  }

  getLastUpdated(novel: NovelDto): string {
    return moment(novel.updatedAt).fromNow();
  }

  // https://stackoverflow.com/questions/3426404/create-a-hexadecimal-colour-based-on-a-string-with-javascript
  guidToColor(guid: string): string {
    let hash = 0;
    for (let i = 0; i < guid.length; i++) {
      hash = guid.charCodeAt(i) + ((hash << 5) - hash);
      hash = hash & hash; // Convert to 32bit integer
    }

    let color = '#';
    for (let i = 0; i < 3; i++) {
      const value = (hash >> (i * 8)) & 0xff;
      color += ('00' + value.toString(16)).slice(-2);
    }

    // Add some transparency
    color += '50';

    return color;
  }

  openCreateNovelDialog(): void {
    const dialogRef = this.dialog.open(CreateNovelComponent, {
      minWidth: '50vw',
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.getNovels();
      }
    });
  }
}
