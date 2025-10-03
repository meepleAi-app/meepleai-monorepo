import { useState } from 'react';
import Link from 'next/link';

interface PdfDocument {
  id: string;
  fileName: string;
  fileSizeBytes: number;
  uploadedAt: string;
  uploadedByUserId: string;
}

export default function UploadPage() {
  const [gameId, setGameId] = useState('demo');
  const [file, setFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);
  const [message, setMessage] = useState('');
  const [pdfs, setPdfs] = useState<PdfDocument[]>([]);
  const [loadingPdfs, setLoadingPdfs] = useState(false);

  const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files[0]) {
      setFile(e.target.files[0]);
      setMessage('');
    }
  };

  const handleUpload = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!file) {
      setMessage('Please select a PDF file');
      return;
    }

    if (!gameId.trim()) {
      setMessage('Please enter a game ID');
      return;
    }

    setUploading(true);
    setMessage('');

    try {
      const formData = new FormData();
      formData.append('file', file);
      formData.append('gameId', gameId);

      const response = await fetch(`${API_BASE}/ingest/pdf`, {
        method: 'POST',
        body: formData,
        credentials: 'include',
      });

      if (response.ok) {
        const data = await response.json();
        setMessage(`✅ PDF uploaded successfully! Document ID: ${data.documentId}`);
        setFile(null);
        // Reset file input
        const fileInput = document.getElementById('fileInput') as HTMLInputElement;
        if (fileInput) {
          fileInput.value = '';
        }
        // Reload PDFs
        loadPdfs();
      } else {
        const error = await response.json();
        setMessage(`❌ Upload failed: ${error.error || response.statusText}`);
      }
    } catch (error) {
      setMessage(`❌ Upload failed: ${error instanceof Error ? error.message : 'Unknown error'}`);
    } finally {
      setUploading(false);
    }
  };

  const loadPdfs = async () => {
    if (!gameId.trim()) {
      return;
    }

    setLoadingPdfs(true);
    try {
      const response = await fetch(`${API_BASE}/games/${gameId}/pdfs`, {
        credentials: 'include',
      });

      if (response.ok) {
        const data = await response.json();
        setPdfs(data.pdfs || []);
      } else {
        console.error('Failed to load PDFs:', response.statusText);
      }
    } catch (error) {
      console.error('Failed to load PDFs:', error);
    } finally {
      setLoadingPdfs(false);
    }
  };

  const formatFileSize = (bytes: number) => {
    if (bytes < 1024) {
      return bytes + ' B';
    }
    if (bytes < 1024 * 1024) {
      return (bytes / 1024).toFixed(1) + ' KB';
    }
    return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
  };

  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleString();
  };

  return (
    <div style={{ padding: '40px', maxWidth: '800px', margin: '0 auto', fontFamily: 'system-ui, sans-serif' }}>
      <div style={{ marginBottom: '20px' }}>
        <Link href="/" style={{ color: '#0070f3', textDecoration: 'none' }}>
          ← Back to Home
        </Link>
      </div>

      <h1 style={{ marginBottom: '30px' }}>Upload PDF Rulebook</h1>

      <form onSubmit={handleUpload} style={{ marginBottom: '40px' }}>
        <div style={{ marginBottom: '20px' }}>
          <label htmlFor="gameId" style={{ display: 'block', marginBottom: '8px', fontWeight: '500' }}>
            Game ID:
          </label>
          <input
            id="gameId"
            type="text"
            value={gameId}
            onChange={(e) => setGameId(e.target.value)}
            placeholder="Enter game ID (e.g., demo)"
            style={{
              width: '100%',
              padding: '12px',
              border: '1px solid #ddd',
              borderRadius: '4px',
              fontSize: '16px',
            }}
          />
        </div>

        <div style={{ marginBottom: '20px' }}>
          <label htmlFor="fileInput" style={{ display: 'block', marginBottom: '8px', fontWeight: '500' }}>
            PDF File:
          </label>
          <input
            id="fileInput"
            type="file"
            accept="application/pdf"
            onChange={handleFileChange}
            style={{
              width: '100%',
              padding: '12px',
              border: '1px solid #ddd',
              borderRadius: '4px',
              fontSize: '16px',
            }}
          />
          {file && (
            <p style={{ marginTop: '8px', fontSize: '14px', color: '#666' }}>
              Selected: {file.name} ({formatFileSize(file.size)})
            </p>
          )}
        </div>

        <button
          type="submit"
          disabled={uploading || !file}
          style={{
            padding: '12px 24px',
            backgroundColor: uploading || !file ? '#ccc' : '#0070f3',
            color: 'white',
            border: 'none',
            borderRadius: '4px',
            fontSize: '16px',
            cursor: uploading || !file ? 'not-allowed' : 'pointer',
            fontWeight: '500',
          }}
        >
          {uploading ? 'Uploading...' : 'Upload PDF'}
        </button>
      </form>

      {message && (
        <div
          style={{
            padding: '16px',
            backgroundColor: message.startsWith('✅') ? '#e8f5e9' : '#ffebee',
            borderRadius: '4px',
            marginBottom: '20px',
            fontSize: '14px',
          }}
        >
          {message}
        </div>
      )}

      <div style={{ marginTop: '40px' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px' }}>
          <h2 style={{ margin: 0 }}>Uploaded PDFs for Game: {gameId}</h2>
          <button
            onClick={loadPdfs}
            disabled={loadingPdfs}
            style={{
              padding: '8px 16px',
              backgroundColor: loadingPdfs ? '#ccc' : '#0070f3',
              color: 'white',
              border: 'none',
              borderRadius: '4px',
              cursor: loadingPdfs ? 'not-allowed' : 'pointer',
              fontSize: '14px',
            }}
          >
            {loadingPdfs ? 'Loading...' : 'Refresh'}
          </button>
        </div>

        {pdfs.length === 0 ? (
          <p style={{ color: '#666' }}>No PDFs uploaded yet. Click Refresh to load PDFs.</p>
        ) : (
          <table style={{ width: '100%', borderCollapse: 'collapse' }}>
            <thead>
              <tr style={{ backgroundColor: '#f5f5f5', textAlign: 'left' }}>
                <th style={{ padding: '12px', borderBottom: '2px solid #ddd' }}>File Name</th>
                <th style={{ padding: '12px', borderBottom: '2px solid #ddd' }}>Size</th>
                <th style={{ padding: '12px', borderBottom: '2px solid #ddd' }}>Uploaded At</th>
              </tr>
            </thead>
            <tbody>
              {pdfs.map((pdf) => (
                <tr key={pdf.id} style={{ borderBottom: '1px solid #eee' }}>
                  <td style={{ padding: '12px' }}>{pdf.fileName}</td>
                  <td style={{ padding: '12px' }}>{formatFileSize(pdf.fileSizeBytes)}</td>
                  <td style={{ padding: '12px' }}>{formatDate(pdf.uploadedAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}