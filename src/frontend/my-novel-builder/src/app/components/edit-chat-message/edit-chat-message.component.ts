import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DynamicDialogRef, DynamicDialogConfig } from 'primeng/dynamicdialog';
import { ButtonModule } from 'primeng/button';
import { TextareaModule } from 'primeng/textarea';

@Component({
  selector: 'app-edit-chat-message',
  standalone: true,
  imports: [FormsModule, ButtonModule, TextareaModule],
  templateUrl: './edit-chat-message.component.html',
  styleUrls: ['./edit-chat-message.component.scss'],
})
export class EditChatMessageComponent implements OnInit {
  private dialogRef = inject(DynamicDialogRef);
  private config = inject(DynamicDialogConfig);

  messageText = '';

  ngOnInit(): void {
    if (this.config.data && this.config.data.text) {
      this.messageText = this.config.data.text;
    }
  }

  save(): void {
    this.dialogRef.close(this.messageText);
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
