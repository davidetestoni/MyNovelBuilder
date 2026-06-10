import { Component, inject, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import moment from 'moment';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { Tooltip } from 'primeng/tooltip';
import { CreateWorldBuildingSessionComponent } from '../../components/create-world-building-session/create-world-building-session.component';
import { WorldBuilderSessionComponent } from '../../components/world-builder-session/world-builder-session.component';
import { WorldBuildingSessionService } from '../../services/world-building-session.service';
import {
  WorldBuildingSession,
  WorldBuildingSessionMetadata,
} from '../../types/dtos/world-building/world-building-session';

@Component({
  selector: 'app-world-builder',
  standalone: true,
  templateUrl: './world-builder.component.html',
  styleUrls: ['./world-builder.component.scss'],
  imports: [RouterModule, Tooltip, ConfirmDialogModule, WorldBuilderSessionComponent],
  providers: [ConfirmationService, DialogService],
})
export class WorldBuilderComponent implements OnInit, OnDestroy {
  sessions: WorldBuildingSessionMetadata[] | null = null;
  currentSessionId: string | null = null;
  currentSession: WorldBuildingSession | null = null;

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private dialogService = inject(DialogService);
  private dialogRef: DynamicDialogRef | null = null;

  readonly worldBuildingSessionService = inject(WorldBuildingSessionService);
  readonly confirmationService = inject(ConfirmationService);

  ngOnInit(): void {
    this.getSessions();
    this.route.paramMap.subscribe((params) => {
      const id = params.get('id');
      if (id) {
        this.loadSession(id);
      } else {
        this.currentSessionId = null;
        this.currentSession = null;
        this.maybeOpenFirstSession();
      }
    });
  }

  ngOnDestroy(): void {
    this.dialogRef?.close();
  }

  getSessions(): void {
    this.worldBuildingSessionService.getSessions().subscribe((sessions) => {
      this.sessions = sessions;
      this.maybeOpenFirstSession();
    });
  }

  loadSession(sessionId: string): void {
    if (this.currentSessionId !== sessionId) {
      this.worldBuildingSessionService.getSession(sessionId).subscribe((session) => {
        this.currentSession = session;
        this.currentSessionId = sessionId;
      });
    }
  }

  selectSession(sessionId: string): void {
    this.router.navigate(['/world-builder', sessionId]);
  }

  deleteSession(sessionId: string): void {
    this.confirmationService.confirm({
      message: 'Are you sure you want to delete this world-building session?',
      header: 'Confirm Delete',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.worldBuildingSessionService.deleteSession(sessionId).subscribe(() => {
          this.sessions =
            this.sessions?.filter((session) => session.id !== sessionId) ?? null;
          if (this.currentSessionId === sessionId) {
            this.router.navigate(['/world-builder']);
          }
        });
      },
    });
  }

  openCreateSessionDialog(): void {
    this.dialogRef = this.dialogService.open(CreateWorldBuildingSessionComponent, {
      header: 'Create World Builder Session',
      width: '520px',
      modal: true,
      closable: true,
      closeOnEscape: true,
      dismissableMask: true,
    });

    this.dialogRef?.onClose.subscribe((result: WorldBuildingSession | undefined) => {
      if (!result) {
        return;
      }

      const metadata: WorldBuildingSessionMetadata = {
        id: result.id,
        novelId: result.context.novelId,
        name: result.name,
        createdAt: result.createdAt,
        updatedAt: result.updatedAt,
      };
      this.sessions = [metadata, ...(this.sessions ?? [])];
      this.router.navigate(['/world-builder', result.id]);
    });
  }

  updateLocalSessionMetadata(session: WorldBuildingSession): void {
    this.currentSession = session;

    if (this.sessions === null) {
      return;
    }

    const metadata = this.sessions.find((item) => item.id === session.id);
    if (metadata) {
      metadata.name = session.name;
      metadata.novelId = session.context.novelId;
      metadata.updatedAt = new Date().toISOString();
    }
  }

  getLastUpdated(session: WorldBuildingSessionMetadata): string {
    return moment(session.updatedAt).fromNow();
  }

  private maybeOpenFirstSession(): void {
    if (
      this.sessions &&
      this.sessions.length > 0 &&
      !this.route.snapshot.paramMap.get('id')
    ) {
      this.selectSession(this.sessions[0].id);
    }
  }
}
