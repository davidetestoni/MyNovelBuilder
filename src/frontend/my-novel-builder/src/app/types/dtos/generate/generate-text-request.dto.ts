export interface GenerateTextRequestDto {
  model: string;
  context: string | null;
  instructions: string | null;
  promptId: string;
  novelId: string;
}
