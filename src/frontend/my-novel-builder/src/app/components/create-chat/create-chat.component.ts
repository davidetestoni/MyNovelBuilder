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

  formGroup = new FormGroup({
    novel: new FormControl<NovelDto | null>(null, [Validators.required]),
  });

  ngOnInit(): void {
    this.novelService.getNovels().subscribe((novels) => {
      this.novels = novels;
    });
  }

  createChat(): void {
    if (this.formGroup.valid) {
      const selectedNovel = this.formGroup.get('novel')?.value;
      if (selectedNovel) {
        this.chatService
          .createChat({ novelId: selectedNovel.id })
          .subscribe((chat) => {
            this.toastr.success('Chat created successfully.');
            this.dialogRef.close(chat);
          });
      }
    }
  }
}
