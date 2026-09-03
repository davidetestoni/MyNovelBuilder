export class NdjsonStreamDecoder<T> {
  private receivedText = '';
  private bufferedText = '';

  get rawResponse(): string {
    return this.receivedText;
  }

  pushCumulative(responseText: string, flush = false): T[] {
    if (!responseText.startsWith(this.receivedText)) {
      throw new Error('The streamed response changed after it was received.');
    }

    this.bufferedText += responseText.slice(this.receivedText.length);
    this.receivedText = responseText;

    const records = this.bufferedText.split('\n');
    this.bufferedText = records.pop() ?? '';

    if (flush && this.bufferedText.length > 0) {
      records.push(this.bufferedText);
      this.bufferedText = '';
    }

    return records
      .map((record) => record.trim())
      .filter((record) => record.length > 0)
      .map((record) => JSON.parse(record) as T);
  }
}
