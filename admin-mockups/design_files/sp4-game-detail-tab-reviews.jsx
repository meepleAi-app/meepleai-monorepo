/**
 * sp4-game-detail-tab-reviews.jsx — AI placeholder (#2148)
 *
 * Friend-first commentary M1 variant for the `/games/[id]/reviews` sub-tab.
 * Friends section rendered ABOVE community per audit
 * `2026-06-10-mockup-coverage-gap-report.md`.
 *
 * Designer review required: polish typography, add state variants
 * (loading/empty/error), pin acceptance values.
 *
 * Parent canonical mockup: sp4-game-detail.html.
 */

function ReviewsTab({ friendReviews = SAMPLE_FRIENDS, communityReviews = SAMPLE_COMMUNITY }) {
  return (
    <section data-tab="reviews" aria-label="Recensioni">
      <header className="tab-header">
        <h1>
          Recensioni{' '}
          <span className="entity-chip e-game" aria-hidden>
            🎲 Game
          </span>
        </h1>
        <p className="subtitle">Cosa pensano i tuoi amici e la community di questo gioco.</p>
      </header>

      <h2 className="section-title" id="friends-reviews">
        👥 Dai tuoi amici
      </h2>
      {friendReviews.length === 0 ? (
        <EmptyFriends />
      ) : (
        <ul aria-labelledby="friends-reviews">
          {friendReviews.map(r => (
            <ReviewCard key={r.id} review={r} isFriend />
          ))}
        </ul>
      )}

      <h2 className="section-title" id="community-reviews">
        🌐 Dalla community
      </h2>
      <ul aria-labelledby="community-reviews">
        {communityReviews.map(r => (
          <ReviewCard key={r.id} review={r} isFriend={false} />
        ))}
      </ul>
    </section>
  );
}

function ReviewCard({ review, isFriend }) {
  return (
    <li className="review-card">
      <span className="avatar" aria-hidden>
        {review.initials}
      </span>
      <div className="review-body">
        <div className="review-header">
          <span className="reviewer-name">{review.name}</span>
          {isFriend && <span className="friend-pip">amico</span>}
          <span className="rating">★ {review.rating.toFixed(1)}</span>
        </div>
        <p className="review-text">{review.text}</p>
        <span className="review-meta">
          {review.date} · {review.playedCount > 0 ? `${review.playedCount} partite` : 'community'}
        </span>
      </div>
    </li>
  );
}

function EmptyFriends() {
  return (
    <p className="empty-friends" role="status">
      Nessun amico ha ancora recensito questo gioco. Sii il primo!
    </p>
  );
}

const SAMPLE_FRIENDS = [
  { id: 'f1', name: 'Luigi', initials: 'LU', rating: 4.2, text: "Bel gioco, ma le prime partite sono un po' lente. Dopo si vola.", date: '2 giorni fa', playedCount: 3 },
  { id: 'f2', name: 'Marco', initials: 'MA', rating: 4.8, text: "Capolavoro. Ogni partita è diversa, scelte significative dall'inizio.", date: '1 settimana fa', playedCount: 12 },
];

const SAMPLE_COMMUNITY = [
  { id: 'c1', name: 'Anna_2024', initials: 'AN', rating: 4.5, text: 'Setup un po\' lungo, ma il gameplay ripaga ampiamente.', date: '3 settimane fa', playedCount: 0 },
];

ReactDOM.createRoot(document.getElementById('root')).render(<ReviewsTab />);
