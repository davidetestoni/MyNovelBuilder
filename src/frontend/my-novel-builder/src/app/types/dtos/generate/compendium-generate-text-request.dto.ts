export interface CompendiumGenerateTextRequestDto {
  model: string;
  promptId: string;
  compendiumId: string;
  contextInfo: CompendiumTextGenerationContextInfoDto;
}

export interface CompendiumTextGenerationContextInfoDto {
  $type: CompendiumTextGenerationType;
}

export interface DescribeImageContextInfoDto
  extends CompendiumTextGenerationContextInfoDto {
  $type: CompendiumTextGenerationType.DescribeImage;
  instructions: string | null;
}

export interface CreateCompendiumRecordImageGenerationPromptContextInfoDto
  extends CompendiumTextGenerationContextInfoDto {
  $type: CompendiumTextGenerationType.CreateCompendiumRecordImageGenerationPrompt;
  compendiumRecordId: string;
  instructions: string | null;
}

export enum CompendiumTextGenerationType {
  DescribeImage = 'describeImage',
  CreateCompendiumRecordImageGenerationPrompt = 'createCompendiumRecordImageGenerationPrompt',
}
