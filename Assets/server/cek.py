import whisper

model = whisper.load_model("small")
result = model.transcribe("contoh_audio.wav", language="id")

print("Transkripsi:", result['text'])
