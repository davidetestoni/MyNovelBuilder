import {
  AfterViewChecked,
  Component,
  ElementRef,
  EventEmitter,
  Input,
  OnChanges,
  OnInit,
  OnDestroy,
  Output,
  SimpleChanges,
  ViewChild,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { MarkdownComponent } from 'ngx-markdown';
import { ToastrService } from 'ngx-toastr';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { MultiSelect } from 'primeng/multiselect';
import { Select } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { CompendiumOptionPreviewComponent } from '../compendium-option-preview/compendium-option-preview.component';
import { ModelSelectComponent } from '../model-select/model-select.component';
import { PromptSelectComponent } from '../prompt-select/prompt-select.component';
import { OptionPreviewComponent } from '../option-preview/option-preview.component';
import { EditChatMessageComponent } from '../edit-chat-message/edit-chat-message.component';
import { CompendiumService } from '../../services/compendium.service';
import { LocalStorageService } from '../../services/local-storage.service';
import { NovelService } from '../../services/novel.service';
import { WorldBuildingSessionService } from '../../services/world-building-session.service';
import { CompendiumDto } from '../../types/dtos/compendium/compendium.dto';
import { NovelDto } from '../../types/dtos/novel/novel.dto';
import { Prose } from '../../types/dtos/novel/prose';
import {
  WorldBuildingOperationKind,
  WorldBuildingProposal,
  WorldBuildingProposalStatus,
  WorldBuildingSession,
  WorldBuildingMessage,
} from '../../types/dtos/world-building/world-building-session';
import { ChatMessageRole } from '../../types/enums/chat-message-role';
import { CompendiumRecordType } from '../../types/enums/compendium-record-type';
import { LocalStorageKey } from '../../types/enums/local-storage-key';
import { PromptType } from '../../types/enums/prompt-type';
import { v4 as uuidv4 } from 'uuid';

@Component({
  selector: 'app-world-builder-session',
  standalone: true,
  templateUrl: './world-builder-session.component.html',
  styleUrls: ['./world-builder-session.component.scss'],
  imports: [
    FormsModule,
    RouterModule,
    MultiSelect,
    Select,
    TextareaModule,
    MarkdownComponent,
    ModelSelectComponent,
    PromptSelectComponent,
    CompendiumOptionPreviewComponent,
    OptionPreviewComponent,
    ConfirmDialogModule,
  ],
  providers: [ConfirmationService, DialogService],
})
export class WorldBuilderSessionComponent
  implements OnInit, OnChanges, AfterViewChecked, OnDestroy
{
  @Input() currentSessionId!: string;
  @Input() currentSession!: WorldBuildingSession;
  @Output() sessionUpdated = new EventEmitter<WorldBuildingSession>();
  @ViewChild('messagesContainer') private messagesContainer!: ElementRef;

  private novelService = inject(NovelService);
  private compendiumService = inject(CompendiumService);
  private worldBuildingSessionService = inject(WorldBuildingSessionService);
  private localStorageService = inject(LocalStorageService);
  private toastr = inject(ToastrService);
  private confirmationService = inject(ConfirmationService);
  private dialogService = inject(DialogService);
  private dialogRef: DynamicDialogRef | null = null;

  ChatMessageRole = ChatMessageRole;
  PromptType = PromptType;
  WorldBuildingProposalStatus = WorldBuildingProposalStatus;
  WorldBuildingOperationKind = WorldBuildingOperationKind;
  CompendiumRecordType = CompendiumRecordType;

  novels = signal<NovelDto[]>([]);
  prose = signal<Prose | null>(null);
  compendia = signal<CompendiumDto[]>([]);

  selectedModel: string | null = null;
  selectedPromptId: string | null = null;
  promptCount = -1;
  userInput = '';
  isGenerating = false;
  failedMessageIds = new Set<string>();
  private expandedProposalIds = new Set<string>();
  private collapsedProposalIds = new Set<string>();
  private shouldScrollToBottom = false;

  recordTypeOptions = Object.values(CompendiumRecordType);

  chapters = computed(() => {
    const prose = this.prose();
    if (!prose) {
      return [];
    }

    return prose.chapters.map((chapter, index) => ({
      label: chapter.title,
      value: index,
    }));
  });

  allAvailableRecords = computed(() => {
    return this.compendia().flatMap((compendium) => compendium.records);
  });

  getOperationKindLabel(kind: WorldBuildingOperationKind): string {
    switch (kind) {
      case WorldBuildingOperationKind.CreateCompendium:
        return 'Create compendium';
      case WorldBuildingOperationKind.UpdateCompendium:
        return 'Update compendium';
      case WorldBuildingOperationKind.CreateCompendiumRecord:
        return 'Create compendium record';
      case WorldBuildingOperationKind.UpdateCompendiumRecord:
        return 'Update compendium record';
    }
  }

  getProposalStatusLabel(status: WorldBuildingProposalStatus): string {
    switch (status) {
      case WorldBuildingProposalStatus.Pending:
        return 'Pending';
      case WorldBuildingProposalStatus.Accepted:
        return 'Accepted';
      case WorldBuildingProposalStatus.Rejected:
        return 'Rejected';
    }
  }

  getRecordTypeLabel(type: CompendiumRecordType): string {
    return type.charAt(0).toUpperCase() + type.slice(1);
  }

  ngOnInit(): void {
    this.novelService.getNovels().subscribe((novels) => {
      this.novels.set(novels);
    });
    this.compendiumService.getCompendia().subscribe((compendia) => {
      this.compendia.set(compendia);
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['currentSession'] && this.currentSession) {
      this.failedMessageIds = new Set<string>();
      this.expandedProposalIds = new Set<string>();
      this.collapsedProposalIds = new Set<string>();
      this.loadProseContext();
      this.shouldScrollToBottom = true;
    }
  }

  ngAfterViewChecked(): void {
    if (this.shouldScrollToBottom) {
      this.scrollToBottom();
    }
  }

  ngOnDestroy(): void {
    this.dialogRef?.close();
  }

  sendMessage(): void {
    if (
      !this.userInput.trim() ||
      this.isGenerating ||
      !this.selectedModel ||
      !this.selectedPromptId
    ) {
      return;
    }

    const message = this.userInput.trim();
    this.userInput = '';

    const localMessage: WorldBuildingMessage = {
      id: uuidv4(),
      sentAt: new Date().toISOString(),
      role: ChatMessageRole.User,
      textContent: message,
    };
    this.currentSession.messages = [...this.currentSession.messages, localMessage];
    this.shouldScrollToBottom = true;

    this.executeSend(message, localMessage.id);
  }

  retryMessage(message: WorldBuildingMessage): void {
    if (
      this.isGenerating ||
      message.role !== ChatMessageRole.User ||
      !this.selectedModel ||
      !this.selectedPromptId
    ) {
      return;
    }

    this.failedMessageIds.delete(message.id);
    this.failedMessageIds = new Set(this.failedMessageIds);
    this.executeSend(message.textContent, message.id);
  }

  isFailedMessage(messageId: string): boolean {
    return this.failedMessageIds.has(messageId);
  }

  editMessage(message: WorldBuildingMessage): void {
    this.dialogRef = this.dialogService.open(EditChatMessageComponent, {
      header: 'Edit Message',
      width: '50vw',
      data: {
        text: message.textContent,
      },
      modal: true,
      closable: true,
      dismissableMask: true,
    });

    this.dialogRef?.onClose.subscribe((newText: string | undefined) => {
      if (newText !== undefined && newText !== message.textContent) {
        message.textContent = newText;
        this.saveSession();
      }
    });
  }

  deleteMessage(message: WorldBuildingMessage): void {
    const isAssistant = message.role === ChatMessageRole.Assistant;
    this.confirmationService.confirm({
      message: isAssistant
        ? 'Delete this assistant message and its proposals?'
        : 'Delete this message?',
      header: 'Confirm Delete',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        if (this.failedMessageIds.has(message.id)) {
          this.failedMessageIds.delete(message.id);
          this.failedMessageIds = new Set(this.failedMessageIds);
          this.currentSession.messages = this.currentSession.messages.filter(
            (item) => item.id !== message.id,
          );
          this.sessionUpdated.emit(this.currentSession);
          return;
        }

        this.worldBuildingSessionService
          .deleteMessage(this.currentSessionId, message.id)
          .subscribe((session) => {
            this.currentSession = session;
            this.sessionUpdated.emit(session);
          });
      },
    });
  }

  updateSessionName(name: string | null): void {
    this.currentSession.name = name?.trim() || null;
    this.saveSession();
  }

  updatePremise(): void {
    this.currentSession.context.freeformPremise =
      this.currentSession.context.freeformPremise?.trim() || null;
    this.saveSession();
  }

  updateNovel(novelId: string | null): void {
    this.currentSession.context.novelId = novelId;
    this.currentSession.context.chapterIndex = null;
    this.loadProseContext();
    this.saveSession();
  }

  updateChapter(chapterIndex: number | null): void {
    this.currentSession.context.chapterIndex = chapterIndex;
    this.saveSession();
  }

  onCompendiaChange(event: { value: string[] }): void {
    const selectedIds = event.value;
    const previousIds = this.currentSession.context.compendiumIds;
    const added = selectedIds.filter((id) => !previousIds.includes(id));
    const recordIds = [...this.currentSession.context.compendiumRecordIds];

    for (const compendiumId of added) {
      const compendium = this.compendia().find((item) => item.id === compendiumId);
      for (const record of compendium?.records ?? []) {
        if (!recordIds.includes(record.id)) {
          recordIds.push(record.id);
        }
      }
    }

    this.currentSession.context.compendiumIds = selectedIds;
    this.currentSession.context.compendiumRecordIds = recordIds;
    this.saveSession();
  }

  onRecordsChange(event: { value: string[] }): void {
    this.currentSession.context.compendiumRecordIds = event.value;
    this.saveSession();
  }

  getProposalsForMessage(messageId: string): WorldBuildingProposal[] {
    return this.currentSession.proposals.filter(
      (proposal) => proposal.messageId === messageId,
    );
  }

  isRecordOperation(proposal: WorldBuildingProposal): boolean {
    return (
      proposal.operation.kind === WorldBuildingOperationKind.CreateCompendiumRecord ||
      proposal.operation.kind === WorldBuildingOperationKind.UpdateCompendiumRecord
    );
  }

  isCompendiumOperation(proposal: WorldBuildingProposal): boolean {
    return (
      proposal.operation.kind === WorldBuildingOperationKind.CreateCompendium ||
      proposal.operation.kind === WorldBuildingOperationKind.UpdateCompendium
    );
  }

  isProposalCollapsed(proposal: WorldBuildingProposal): boolean {
    if (this.expandedProposalIds.has(proposal.id)) {
      return false;
    }

    if (this.collapsedProposalIds.has(proposal.id)) {
      return true;
    }

    return proposal.status !== WorldBuildingProposalStatus.Pending;
  }

  canAcceptProposal(proposal: WorldBuildingProposal): boolean {
    return this.getAcceptDisabledReason(proposal) === null;
  }

  getAcceptDisabledReason(proposal: WorldBuildingProposal): string | null {
    if (proposal.status !== WorldBuildingProposalStatus.Pending) {
      return 'Only pending proposals can be accepted';
    }

    switch (proposal.operation.kind) {
      case WorldBuildingOperationKind.UpdateCompendium:
      case WorldBuildingOperationKind.CreateCompendiumRecord:
        return proposal.operation.targetCompendiumId
          ? null
          : 'Select a target compendium before accepting';
      case WorldBuildingOperationKind.UpdateCompendiumRecord:
        return proposal.operation.targetRecordId
          ? null
          : 'Select a target record before accepting';
      case WorldBuildingOperationKind.CreateCompendium:
        return null;
    }
  }

  toggleProposal(proposal: WorldBuildingProposal): void {
    if (this.isProposalCollapsed(proposal)) {
      this.collapsedProposalIds.delete(proposal.id);
      this.expandedProposalIds.add(proposal.id);
      return;
    }

    this.expandedProposalIds.delete(proposal.id);
    this.collapsedProposalIds.add(proposal.id);
  }

  saveProposal(proposal: WorldBuildingProposal): void {
    this.worldBuildingSessionService
      .updateProposal(this.currentSessionId, proposal.id, {
        operation: proposal.operation,
        rationale: proposal.rationale,
      })
      .subscribe((session) => {
        this.currentSession = session;
        this.sessionUpdated.emit(session);
        this.toastr.success('Proposal updated');
      });
  }

  acceptProposal(proposal: WorldBuildingProposal): void {
    const disabledReason = this.getAcceptDisabledReason(proposal);

    if (disabledReason) {
      this.toastr.warning(disabledReason);
      return;
    }

    this.worldBuildingSessionService
      .updateProposal(this.currentSessionId, proposal.id, {
        operation: proposal.operation,
        rationale: proposal.rationale,
      })
      .subscribe((updatedSession) => {
        const savedProposal = updatedSession.proposals.find(
          (item) => item.id === proposal.id,
        );

        if (!savedProposal) {
          this.currentSession = updatedSession;
          this.sessionUpdated.emit(updatedSession);
          this.toastr.error('Proposal was not found after saving.');
          return;
        }

        this.worldBuildingSessionService
          .acceptProposal(this.currentSessionId, savedProposal.id)
          .subscribe((acceptedSession) => {
            this.currentSession = acceptedSession;
            this.sessionUpdated.emit(acceptedSession);
            this.refreshCompendia();
            this.toastr.success('Proposal applied');
          });
      });
  }

  rejectProposal(proposal: WorldBuildingProposal): void {
    this.worldBuildingSessionService
      .rejectProposal(this.currentSessionId, proposal.id)
      .subscribe((session) => {
        this.currentSession = session;
        this.sessionUpdated.emit(session);
      });
  }

  copyMessage(textContent: string): void {
    navigator.clipboard.writeText(textContent).then(() => {
      this.toastr.success('Message copied to clipboard');
    });
  }

  onPromptOptionsChanged(count: number): void {
    this.promptCount = count;
  }

  getTargetCompendiumName(compendiumId: string | null): string {
    if (!compendiumId) {
      return 'No compendium';
    }

    return (
      this.compendia().find((compendium) => compendium.id === compendiumId)?.name ??
      compendiumId
    );
  }

  getTargetRecordName(recordId: string | null): string {
    if (!recordId) {
      return 'No record';
    }

    return (
      this.allAvailableRecords().find((record) => record.id === recordId)?.name ??
      recordId
    );
  }

  private executeSend(message: string, localMessageId: string): void {
    if (!this.selectedModel || !this.selectedPromptId) {
      return;
    }

    this.isGenerating = true;
    this.localStorageService.setNestedStringForKey(
      LocalStorageKey.RecentPrompts,
      PromptType.WorldBuildingAgent,
      this.selectedPromptId,
    );

    this.worldBuildingSessionService
      .sendMessage(this.currentSessionId, {
        model: this.selectedModel,
        promptId: this.selectedPromptId,
        message,
      })
      .subscribe({
        next: (session) => {
          this.failedMessageIds.delete(localMessageId);
          this.failedMessageIds = new Set(this.failedMessageIds);
          this.currentSession = session;
          this.isGenerating = false;
          this.shouldScrollToBottom = true;
          this.sessionUpdated.emit(session);
        },
        error: () => {
          this.failedMessageIds.add(localMessageId);
          this.failedMessageIds = new Set(this.failedMessageIds);
          this.isGenerating = false;
          this.toastr.error('Message failed. You can retry it.');
        },
      });
  }

  private saveSession(): void {
    this.worldBuildingSessionService
      .updateSession(this.currentSessionId, {
        name: this.currentSession.name,
        novelId: this.currentSession.context.novelId,
        chapterIndex: this.currentSession.context.chapterIndex,
        compendiumIds: this.currentSession.context.compendiumIds,
        compendiumRecordIds: this.currentSession.context.compendiumRecordIds,
        freeformPremise: this.currentSession.context.freeformPremise,
        messages: this.currentSession.messages,
      })
      .subscribe(() => {
        this.sessionUpdated.emit(this.currentSession);
      });
  }

  private loadProseContext(): void {
    const novelId = this.currentSession.context.novelId;
    this.prose.set(null);

    if (!novelId) {
      return;
    }

    this.novelService.getNovelProse(novelId).subscribe({
      next: (prose) => {
        this.prose.set(prose);
      },
      error: () => {
        this.prose.set(null);
      },
    });
  }

  private refreshCompendia(): void {
    this.compendiumService.getCompendia().subscribe((compendia) => {
      this.compendia.set(compendia);
    });
  }

  private scrollToBottom(): void {
    try {
      this.messagesContainer.nativeElement.scrollTop =
        this.messagesContainer.nativeElement.scrollHeight;
      this.shouldScrollToBottom = false;
    } catch {
      this.shouldScrollToBottom = false;
    }
  }
}
