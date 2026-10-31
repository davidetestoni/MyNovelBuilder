import { TtsProvider } from '../../enums/tts-provider';
import { TextGenerationProvider } from '../../enums/text-generation-provider';

/** Per-export selections; omitted values use server defaults. Never updates integrations. */
export interface AudiobookSettings {
  provider?: TtsProvider;
  modelId?: string;
  voiceId?: string;
  enableTextEmphasis?: boolean;
  enableImmersive?: boolean;
  immersivePauseMs?: number;
  textGenerationProvider?: TextGenerationProvider;
  textGenerationModelId?: string;
  immersivePromptId?: string | null;
}

/** Free estimate and identifier for the exact saved inputs retained by the server. */
export interface AudiobookPreview {
  snapshotId: string;
  expiresAt: string;
  snapshotAt: string;
  settings: AudiobookSettings;
  eligibleSections: number;
  eligibleChapters: number;
  /** Total normalized spoken text, including cached sections. */
  totalCharacters: number;
  /** Whitespace-delimited words across all spoken sections, for duration estimates. */
  spokenWords: number;
  skippedSections: number;
  skippedChapters: number;
  reusableSections: number;
  charactersNeedingSynthesis: number;
  warnings: string[];
}

/** Durable job progress; completed chapter counts refer to assembled audio files. */
export interface AudiobookJob {
  id: string;
  novelId: string;
  snapshotAt: string;
  state: 'queued' | 'running' | 'cancelling' | 'cancelled' | 'failed' | 'completed';
  stage: string;
  attemptNumber: number;
  /** Maximum sections generated concurrently; older servers/jobs default to one. */
  maxParallelSections?: number;
  completedSections: number;
  reusedSections: number;
  generatedSections: number;
  failedSections: number;
  skippedSections: number;
  completedChapters: number;
  totalChapters: number;
  downloadAvailable: boolean;
  errorCode: string | null;
  sections: { chapterIndex: number; sectionIndex: number; state: string }[];
  attempts: { number: number; state: string; stage: string; errorCode: string | null; createdAt: string; finishedAt: string | null }[];
}
