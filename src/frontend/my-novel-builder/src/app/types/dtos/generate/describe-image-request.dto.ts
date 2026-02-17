export interface DescribeImageRequestDto {
  model: string;
  promptId: string;
  compendiumId: string;
  instructions: string | null;
}
