import { Component, Inject, OnInit } from '@angular/core';
import { NovelService } from '../../services/novel.service';
import { NovelDto } from '../../types/dtos/novel/novel.dto';
import { EllipsisPipe } from '../../pipes/ellipsis.pipe';
import moment from 'moment';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-novels',
  standalone: true,
  templateUrl: './novels.component.html',
  styleUrls: ['./novels.component.scss'],
  imports: [EllipsisPipe, RouterModule],
})
export class NovelsComponent implements OnInit {
  novels: NovelDto[] | null = null;

  constructor(@Inject(NovelService) private novelService: NovelService) {}

  ngOnInit(): void {
    this.novelService.getNovels().subscribe((novels) => {
      this.novels = novels;
    });
  }

  getNovelCoverImageUrl(novelId: string): string {
    return this.novelService.getNovelCoverImageUrl(novelId);
  }

  // Using moment.js get the time since the last update
  getLastUpdated(novel: NovelDto): string {
    return moment(novel.updatedAt).fromNow();
  }
}
