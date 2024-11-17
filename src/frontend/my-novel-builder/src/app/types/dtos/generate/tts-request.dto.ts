export interface TtsRequestDto {
    modelId: string | null;
    voiceId: string;
    message: string;
}
