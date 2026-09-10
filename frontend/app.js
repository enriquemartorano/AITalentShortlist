const API_BASE = 'http://localhost:5080';

const state = {
  jobDescription: null,
  rankingProfile: null,
  candidates: []
};

const jobDescriptionEl = document.getElementById('jobDescription');
const rankingProfileEl = document.getElementById('rankingProfile');
const candidateListEl = document.getElementById('candidateList');
const evaluationResultEl = document.getElementById('evaluationResult');

const seedBtn = document.getElementById('seedBtn');
const loadCandidatesBtn = document.getElementById('loadCandidatesBtn');
const clearCandidatesBtn = document.getElementById('clearCandidatesBtn');
const evaluateBtn = document.getElementById('evaluateBtn');
const evaluationModeEl = document.getElementById('evaluationMode');

const api = {
  async get(url) {
    const response = await fetch(`${API_BASE}${url}`);
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    return response.json();
  },
  async post(url, body) {
    const response = await fetch(`${API_BASE}${url}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body)
    });
    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(errorText || `HTTP ${response.status}`);
    }
    return response.json();
  },
  async delete(url) {
    const response = await fetch(`${API_BASE}${url}`, { method: 'DELETE' });
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
  }
};

function renderJobDescription() {
  if (!state.jobDescription) {
    jobDescriptionEl.textContent = 'No data yet.';
    return;
  }

  const { title, department, location, description, requiredSkills, preferredSkills, requiredLanguages, minimumYearsOfExperience } = state.jobDescription;
  jobDescriptionEl.innerHTML = `
    <div><strong>Role:</strong> ${title}</div>
    <div><strong>Department:</strong> ${department}</div>
    <div><strong>Location:</strong> ${location}</div>
    <div><strong>Minimum experience:</strong> ${minimumYearsOfExperience} years</div>
    <p>${description}</p>
    <div><strong>Required skills:</strong> ${requiredSkills.join(', ')}</div>
    <div><strong>Preferred skills:</strong> ${preferredSkills.join(', ')}</div>
    <div><strong>Languages:</strong> ${requiredLanguages.join(', ')}</div>
  `;
}

function renderRankingProfile() {
  if (!state.rankingProfile) {
    rankingProfileEl.textContent = 'No data yet.';
    return;
  }

  const rows = state.rankingProfile.criteria
    .map((criterion) => `<li><strong>${criterion.name}</strong> (${criterion.weight}%): ${criterion.keywords.join(', ')}</li>`)
    .join('');

  rankingProfileEl.innerHTML = `
    <div><strong>Name:</strong> ${state.rankingProfile.name}</div>
    <div><strong>Instructions:</strong> ${state.rankingProfile.instructions}</div>
    <ul>${rows}</ul>
  `;
}

function renderCandidates() {
  if (!state.candidates.length) {
    candidateListEl.textContent = 'No candidates.';
    return;
  }

  candidateListEl.innerHTML = state.candidates
    .map((candidate) => `
      <div class="candidate-card">
        <label>
          <input type="checkbox" value="${candidate.id}" checked />
          <div>
            <span class="candidate-name">${candidate.candidateName}</span>
            <span class="candidate-meta">${candidate.fileName}</span>
          </div>
        </label>
      </div>
    `)
    .join('');
}

function renderScoreChart(ranking) {
  if (!ranking.length) return '';

  const maxScore = Math.max(...ranking.map((candidate) => Number(candidate.totalScore) || 0), 100);

  const chart = ranking
    .map((candidate) => {
      const score = Number(candidate.totalScore) || 0;
      const width = Math.max((score / maxScore) * 100, 6);
      return `
        <div class="score-row">
          <div class="score-label">
            <span>${candidate.candidateName}</span>
            <strong>${score}</strong>
          </div>
          <div class="score-bar"><span style="width: ${width}%"></span></div>
        </div>
      `;
    })
    .join('');

  return `
    <div class="chart-panel">
      <h3>Results by candidate</h3>
      ${chart}
    </div>
  `;
}

function renderEvaluationResult(result) {
  if (!result) {
    evaluationResultEl.textContent = 'Select candidates and click evaluate.';
    return;
  }

  const ranking = result.ranking || [];
  const cards = ranking
    .map((candidate) => {
      const recommendation = candidate.recommendation;
      const badgeClass = recommendation === 0 ? 'match' : recommendation === 1 ? 'review' : 'low';
      const recommendationLabel = recommendation === 0 ? 'Strong Match' : recommendation === 1 ? 'Potential Match' : recommendation === 2 ? 'Review Required' : 'Not Recommended';
      const strengths = (candidate.strengths || []).map((item) => `<li>${item}</li>`).join('');
      const gaps = (candidate.gaps || []).map((item) => `<li>${item}</li>`).join('');
      return `
        <div class="result-card">
          <div class="score-line">
            <strong>${candidate.candidateName}</strong>
            <span class="badge ${badgeClass}">${recommendationLabel}</span>
          </div>
          <div><strong>Total:</strong> ${candidate.totalScore}</div>
          <div><strong>Executive summary:</strong> ${candidate.executiveSummary}</div>
          <div><strong>Human review required:</strong> ${candidate.humanReviewRequired ? 'Yes' : 'No'}</div>
          <div><strong>Strengths:</strong><ul>${strengths || '<li>None</li>'}</ul></div>
          <div><strong>Gaps:</strong><ul>${gaps || '<li>None</li>'}</ul></div>
        </div>
      `;
    })
    .join('');

  evaluationResultEl.innerHTML = `
    <div class="result-box">
      <div><strong>Time:</strong> ${result.durationMilliseconds} ms</div>
      <div><strong>Evaluator:</strong> ${result.evaluator}</div>
      ${renderScoreChart(ranking)}
      ${cards}
    </div>
  `;
}

async function clearCandidates() {
  try {
    await api.delete('/api/candidates');
    state.candidates = [];
    renderCandidates();
    evaluationResultEl.innerHTML = '<div class="empty-state">Select candidates and click evaluate.</div>';
  } catch (error) {
    alert(`Unable to clear candidates: ${error.message}`);
  }
}

async function loadDemoData() {
  try {
    seedBtn.disabled = true;
    await api.post('/api/demo/seed', {});
    await Promise.all([
      loadJobDescription(),
      loadRankingProfile(),
      loadCandidates()
    ]);
  } catch (error) {
    alert(`Error cargando datos demo: ${error.message}`);
  } finally {
    seedBtn.disabled = false;
  }
}

async function loadJobDescription() {
  state.jobDescription = await api.get('/api/demo/job-description');
  renderJobDescription();
}

async function loadRankingProfile() {
  state.rankingProfile = await api.get('/api/demo/ranking-profile');
  renderRankingProfile();
}

async function loadCandidates() {
  const data = await api.get('/api/candidates');
  state.candidates = data;
  renderCandidates();
}

async function evaluateSelection() {
  const selected = [...document.querySelectorAll('#candidateList input:checked')].map((input) => input.value);

  if (!selected.length) {
    alert('Select at least one candidate');
    return;
  }

  if (!state.jobDescription || !state.rankingProfile) {
    alert('Load the job description and ranking profile first');
    return;
  }

  evaluateBtn.disabled = true;
  try {
    const evaluationMode = evaluationModeEl.value === 'ai' ? 'Ai' : 'Heuristic';
    const result = await api.post('/api/shortlists/evaluate', {
      jobDescription: state.jobDescription,
      rankingProfile: state.rankingProfile,
      candidateIds: selected,
      evaluationMode
    });
    renderEvaluationResult(result);
  } catch (error) {
    alert(`Evaluation error: ${error.message}`);
  } finally {
    evaluateBtn.disabled = false;
  }
}

seedBtn.addEventListener('click', loadDemoData);
loadCandidatesBtn.addEventListener('click', loadCandidates);
clearCandidatesBtn.addEventListener('click', clearCandidates);
evaluateBtn.addEventListener('click', evaluateSelection);

(async () => {
  try {
    await Promise.all([
      loadJobDescription(),
      loadRankingProfile(),
      loadCandidates()
    ]);
  } catch (error) {
    alert(`Could not connect to the API at ${API_BASE}. Make sure the API is running.`);
  }
})();
