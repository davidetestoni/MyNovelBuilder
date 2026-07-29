import { Component, OnInit, inject } from '@angular/core';
import {
  FormControl,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { NovelService } from '../../services/novel.service';
import { ChatService } from '../../services/chat.service';
import { NovelDto } from '../../types/dtos/novel/novel.dto';
import { DynamicDialogRef } from 'primeng/dynamicdialog';
import { ButtonModule } from 'primeng/button';
import { Select } from 'primeng/select';
import { ToastrService } from 'ngx-toastr';
import { finalize } from 'rxjs';

@Component({
  selector: 'app-create-chat',
  standalone: true,
  imports: [FormsModule, ReactiveFormsModule, ButtonModule, Select],
  templateUrl: './create-chat.component.html',
  styleUrls: ['./create-chat.component.scss'],
})
export class CreateChatComponent implements OnInit {
  private dialogRef = inject(DynamicDialogRef);
  private novelService = inject(NovelService);
  private chatService = inject(ChatService);
  private toastr = inject(ToastrService);

  novels: NovelDto[] | null = null;
  isCreating = false;

  formGroup = new FormGroup({
    novel: new FormControl<NovelDto | null>(null, [Validators.required]),
  });

  ngOnInit(): void {
    this.novelService.getNovels().subscribe({
      next: (novels) => {
        this.novels = novels;
      },
      error: () => {
        this.novels = [];
        this.toastr.error('Could not load novels.');
      },
    });
  }

  createChat(): void {
    if (this.formGroup.invalid || this.isCreating) {
      return;
    }

    const selectedNovel = this.formGroup.controls.novel.value;
    if (!selectedNovel) {
      return;
    }

    this.isCreating = true;
    this.chatService
      .createChat({ novelId: selectedNovel.id })
      .pipe(finalize(() => (this.isCreating = false)))
      .subscribe({
        next: (chat) => {
          this.toastr.success('Chat created successfully.');
          this.dialogRef.close(chat);
        },
        error: () => {
          this.toastr.error('Could not create chat.');
        },
      });
  }
}
