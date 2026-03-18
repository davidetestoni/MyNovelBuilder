export interface DescribeImageRequestDto {
  model: string;
  promptId: string;
  instructions: string | null;
}

export interface DescribeCompendiumImageRequestDto
  extends DescribeImageRequestDto {
  compendiumId: string;
}
