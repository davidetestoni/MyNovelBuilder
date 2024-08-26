import { Component, inject } from '@angular/core';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { NovelDto } from '../../types/dtos/novel/novel.dto';
import { NovelService } from '../../services/novel.service';
import { Prose } from '../../types/dtos/novel/prose';

@Component({
  selector: 'app-novel-editor',
  standalone: true,
  imports: [RouterModule],
  templateUrl: './novel-editor.component.html',
  styleUrl: './novel-editor.component.scss',
})
export class NovelEditorComponent {
  novel: NovelDto | null = null;
  prose: Prose | null = null;
  readonly novelService: NovelService = inject(NovelService);
  novelId!: string;

  constructor(private route: ActivatedRoute) {}

  ngOnInit(): void {
    this.novelId = this.route.snapshot.paramMap.get('id')!;
    this.getNovel();
    this.getProse();
  }

  getNovel(): void {
    this.novelService.getNovel(this.novelId).subscribe((novel) => {
      this.novel = novel;
    });
  }

  getProse(): void {
    this.novelService.getNovelProse(this.novelId).subscribe((prose) => {
      this.prose = prose;
    });
  }
}
