import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { NovelService } from '../../services/novel.service';
import { NovelDto } from '../../types/dtos/novel/novel.dto';
import { EllipsisPipe } from '../../pipes/ellipsis.pipe';
import moment from 'moment';
import { RouterModule } from '@angular/router';
import { ReactiveFormsModule } from '@angular/forms';
import { CreateNovelComponent } from '../../components/create-novel/create-novel.component';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';

@Component({
  selector: 'app-novels',
  standalone: true,
  templateUrl: './novels.component.html',
  styleUrls: ['./novels.component.scss'],
  imports: [EllipsisPipe, RouterModule, ReactiveFormsModule],
  providers: [DialogService],
})
export class NovelsComponent implements OnInit, OnDestroy {
  novels: NovelDto[] | null = null;
  private dialogService = inject(DialogService);
  private dialogRef: DynamicDialogRef | null = null;
  readonly novelService = inject(NovelService);

  ngOnInit(): void {
    this.getNovels();
  }

  ngOnDestroy(): void {
    if (this.dialogRef) {
      this.dialogRef.close();
    }
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
    this.dialogRef = this.dialogService.open(CreateNovelComponent, {
      header: 'Create a novel',
      width: '50vw',
      contentStyle: { overflow: 'auto' },
      baseZIndex: 10000,
      modal: true,
      closable: true,
      closeOnEscape: true,
      dismissableMask: true,
    });

    this.dialogRef?.onClose.subscribe((result) => {
      if (result) {
        this.getNovels();
      }
    });
  }
}