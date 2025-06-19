let selectedReceiver = null;
const backendUrl = "https://localhost:5001";

// Eğer giriş yapılmamışsa sahte kullanıcı ile devam et
const fallbackUser = JSON.parse(localStorage.getItem("user")) || {
    name: "Ziyaretçi",
    email: "anonim@example.com",
    gender: "unknown",
    avatar: ""
};

// Bu fonksiyon sohbet sayfasında çalışır
function loadUser() {

  const token = localStorage.getItem("token");
  if (!token) {
    window.location.href = "index.html";
    return;
  }

  fetch(`${backendUrl}/api/me`, {
    headers: { Authorization: "Bearer " + token }
  })
    .then(res => {
      if (!res.ok) throw new Error("Token geçersiz");
      return res.json();
    })
    .then(data => {
      console.log("Kullanıcı:", data); // ✅ burada email: xxx döner
      // İstersen localStorage.setItem("userEmail", data.email); şeklinde de saklayabilirsin
    })
    .catch(() => {
      localStorage.removeItem("token");
      window.location.href = "index.html";
    });

  const userData = localStorage.getItem("chitchat_user");
if (!userData) {
  window.location.href = "index.html";
  return;
}
const user = JSON.parse(userData);

  $("#profileName").text(user.name);
  $("#profileGender").text(user.gender === "male" ? "Erkek" : "Kadın");
 const avatarPath = user.avatar || (user.gender === "male" ? "img/avatar_male.png" : "img/avatar_female.png");
$("#profileAvatar").attr("src", avatarPath);
 $("#btnProfile").click(function () {
  $("#chatSection").removeClass("active").hide();
  $("#profileSection").addClass("active").fadeIn(200);
  $("#btnChat").removeClass("active");
  $(this).addClass("active");
});

$("#btnChat").click(function () {
  $("#profileSection").removeClass("active").hide();
  $("#chatSection").addClass("active").fadeIn(200);
  $("#btnProfile").removeClass("active");
  $(this).addClass("active");
});

renderUserList(); // kullanıcıları yükle
  loadMessages();
  // Kullanıcı listesini göster


}

function renderUserList(filter = "") {
  const friends = JSON.parse(localStorage.getItem("chitchat_friends")) || [];
  const groups = JSON.parse(localStorage.getItem("chitchat_groups")) || [];
  const filtered = friends.filter(u =>
    u.name.toLowerCase().includes(filter.toLowerCase())
  );
  const blocked = JSON.parse(localStorage.getItem("chitchat_blocked")) || [];
const visible = filtered.filter(u => !blocked.includes(u.email));
let html = ""; // <-- yukarı taşı
if (visible.length === 0) {
  html = `<li class="list-group-item text-muted">Kullanıcı bulunamadı</li>`;
} else {
  visible.forEach(u => {
    // ...
  });
}
  // ✅ Grupları önce listele
groups.forEach(g => {
  html += `
    <li class="list-group-item d-flex align-items-center user-item group-item" data-name="${g.name}" data-email="group:${g.name}">
      <strong>👥 ${g.name}</strong>
    </li>
  `;
});
  if (filtered.length === 0) {
    html = `<li class="list-group-item text-muted">Kullanıcı bulunamadı</li>`;
  } else {
    filtered.forEach(u => {
  const avatar = u.avatar || (u.gender === "male" ? "img/avatar_male.png" : "img/avatar_female.png");
  html += `
    <li class="list-group-item d-flex align-items-center user-item" data-name="${u.name}" data-email="${u.email}">
      <img src="${avatar}" class="rounded-circle me-2" style="width:30px; height:30px;">
      ${u.name}
      <button class="btn btn-sm btn-outline-danger ms-auto" onclick="blockUser('${u.email}')">Engelle</button>
    </li>
  `;
});
  }

  document.getElementById("userList").innerHTML = html;

  document.querySelectorAll(".user-item").forEach(item => {
    item.addEventListener("click", () => {
      document.querySelectorAll(".user-item").forEach(i => i.classList.remove("active-user"));
      item.classList.add("active-user");

      selectedReceiver = {
  name: item.dataset.name,
  email: item.dataset.email
};


if (item.classList.contains("group-item")) {
  document.querySelector("#chatSection .card-header").innerText = `👥 Grup: ${selectedReceiver.name}`;
} else {
  document.querySelector("#chatSection .card-header").innerText = `${selectedReceiver.name} ile Sohbet`;
}
      loadMessages();
    });
  });
}
function blockUser(email) {
  let blocked = JSON.parse(localStorage.getItem("chitchat_blocked")) || [];
  if (!blocked.includes(email)) {
    blocked.push(email);
    localStorage.setItem("chitchat_blocked", JSON.stringify(blocked));
    alert("🚫 Kullanıcı engellendi.");
    renderUserList(); // listeyi güncelle
  }
}
function renderBlockedUsers() {
  const blocked = JSON.parse(localStorage.getItem("chitchat_blocked")) || [];
  const friends = JSON.parse(localStorage.getItem("chitchat_friends")) || [];
  const list = document.getElementById("blockedList");

  if (!list) return;

  list.innerHTML = "";

  if (blocked.length === 0) {
    list.innerHTML = `<li class="list-group-item text-muted">Engellenmiş kullanıcı yok</li>`;
    return;
  }

  blocked.forEach(email => {
    const user = friends.find(f => f.email === email) || { name: email };
    const item = document.createElement("li");
    item.className = "list-group-item d-flex justify-content-between align-items-center";
    item.innerHTML = `
      <span>${user.name} (${email})</span>
      <button class="btn btn-sm btn-outline-secondary" onclick="unblockUser('${email}')">Kaldır</button>
    `;
    list.appendChild(item);
  });
}
function unblockUser(email) {
  let blocked = JSON.parse(localStorage.getItem("chitchat_blocked")) || [];
  blocked = blocked.filter(e => e !== email);
  localStorage.setItem("chitchat_blocked", JSON.stringify(blocked));
  alert("✅ Kullanıcının engeli kaldırıldı.");
  renderUserList();
  renderBlockedUsers(); // listeyi yenile
}


// Arama kutusu olayı
document.addEventListener("DOMContentLoaded", () => {
  const searchInput = document.getElementById("userSearch");
  if (searchInput) {
    searchInput.addEventListener("input", (e) => {
      renderUserList(e.target.value);
    });
  } 
});
function populateFriendCheckboxes() {
  const friends = JSON.parse(localStorage.getItem("chitchat_friends")) || [];
  const container = document.getElementById("friendCheckboxes");
  container.innerHTML = "";

  if (friends.length === 0) {
    container.innerHTML = `<div class="text-muted">Hiç arkadaşınız yok.</div>`;
    return;
  }

  friends.forEach(friend => {
    container.innerHTML += `
      <div class="form-check">
        <input class="form-check-input" type="checkbox" value="${friend.email}" id="friend-${friend.email}">
        <label class="form-check-label" for="friend-${friend.email}">
          ${friend.name} (${friend.email})
        </label>
      </div>
    `;
  });
}

// ✅ DOĞRU: Fonksiyon DOMContentLoaded'ın dışına alınmalı
function createGroup() {
  const name = document.getElementById("groupName").value.trim();
  const checkboxes = document.querySelectorAll("#friendCheckboxes input:checked");
  const members = Array.from(checkboxes).map(cb => cb.value);

  if (!name || members.length === 0) {
    alert("Grup adı girin ve en az 1 kişi seçin.");
    return;
  }

  const creator = JSON.parse(localStorage.getItem("chitchat_user"));
  const group = {
    name,
    members: [creator.email, ...members]
  };

  console.log("📦 Yeni grup oluşturuluyor:", group);
  // localStorage'a grup kaydet
let groups = JSON.parse(localStorage.getItem("chitchat_groups")) || [];
groups.push(group);
localStorage.setItem("chitchat_groups", JSON.stringify(groups));

  alert("✅ Grup oluşturuldu (simülasyon)");

  const modal = bootstrap.Modal.getInstance(document.getElementById("groupModal"));
  modal.hide();
}


function sendMessage() {
  const input = document.getElementById("messageInput");
  const fileInput = document.getElementById("fileInput");
  const text = input.value.trim();
  const file = fileInput.files[0];
  const user = JSON.parse(localStorage.getItem("chitchat_user"));

   console.log("user.name:", user.name);
  console.log("receiver:", selectedReceiver);

  if (!selectedReceiver || (!text && !file)) return;

 const messageData = {
  sender: user.name,
  receiver: selectedReceiver.email,
  gender: user.gender,
  avatar: user.avatar,
  text: text,
  timestamp: new Date().toLocaleString()
};

  if (file) {
    const reader = new FileReader();
    reader.onload = function (e) {
      messageData.fileData = e.target.result;
      messageData.fileName = file.name;
      messageData.fileType = file.type;

      sendToServer(messageData);
    };
    reader.readAsDataURL(file);
  } else {
    sendToServer(messageData);
  }

  function sendToServer(data) {
    fetch(`${backendUrl}/api/messages`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ ...data, avatar: user.avatar })
    })
      .then(async res => {
  const data = await res.json().catch(() => null);
  if (!res.ok) throw new Error(data?.message || "Sunucuya mesaj gönderilemedi");
  return data;
})
      .then(() => {
        input.value = "";
        fileInput.value = "";
        loadMessages();
      })
      .catch(err => {
        console.error("Mesaj gönderme hatası:", err);
        alert("❌ Mesaj gönderilemedi.");
      });
  }
}
document.getElementById("messageInput").addEventListener("keydown", function (e) {
  if (e.key === "Enter" && !e.shiftKey) {
    e.preventDefault(); // Sayfanın yenilenmesini engeller
    sendMessage();      // Mesajı gönder
  }
});

function updateAvatar() {
  const file = document.getElementById("updateAvatarInput").files[0];
  if (!file) {
    alert("Lütfen bir görsel seçin.");
    return;
  }

  const reader = new FileReader();
  reader.onload = function (e) {
    const avatarBase64 = e.target.result;
    const user = JSON.parse(localStorage.getItem("chitchat_user"));
    user.avatar = avatarBase64;
    localStorage.setItem("chitchat_user", JSON.stringify(user));

    $("#profileAvatar").attr("src", avatarBase64);
    alert("✅ Profil fotoğrafı güncellendi.");
  };
  reader.readAsDataURL(file);
}

async function loadMessages() {
  const user = JSON.parse(localStorage.getItem("chitchat_user"));
  const area = document.getElementById("messageArea");
  area.innerHTML = "";

  if (!selectedReceiver) {
    area.innerHTML = "<p class='text-muted'>Bir kullanıcı seçin.</p>";
    return;
  }

  try {
    const res = await fetch(`${backendUrl}/api/messages?user1=${user.email}&user2=${selectedReceiver.email}`);

    const messages = await res.json();

    messages.forEach(msg => {
      const align = msg.sender === user.name ? "text-end" : "text-start";
      const bubbleClass = msg.sender === user.name ? "bg-primary text-white" : "bg-light";
         const avatarImg = msg.avatar
    ? `<img src="${msg.avatar}" class="rounded-circle me-2" style="width:30px; height:30px;">`
    : `<img src="${msg.gender === "male" ? "img/avatar_male.png" : "img/avatar_female.png"}" class="rounded-circle me-2" style="width:30px; height:30px;">`;

      let fileHTML = "";
      if (msg.fileData) {
        if (msg.fileType?.startsWith("image/")) {
          fileHTML = `<img src="${msg.fileData}" alt="${msg.fileName}" style="max-width: 200px;" class="img-fluid rounded mt-2">`;
        } else {
          fileHTML = `<a href="${msg.fileData}" download="${msg.fileName}" class="btn btn-sm btn-outline-secondary mt-2">📎 ${msg.fileName}</a>`;
        }
      }

area.innerHTML += `
  <div class="d-flex ${align} mb-3">
    ${align === "text-end" ? "" : avatarImg}
    <div class="d-inline-block p-2 rounded ${bubbleClass}">
      <strong>${msg.sender}:</strong> ${msg.text || ""}
      ${fileHTML}
      <div class="text-muted small">${msg.timestamp}</div>
    </div>
    ${align === "text-end" ? avatarImg : ""}
  </div>
`;
if (msg.receiver === user.email && msg.sender !== user.name) {
  showNotification(`📨 ${msg.sender} sana mesaj gönderdi.`);
}
    });

    area.scrollTo({ top: area.scrollHeight, behavior: "smooth" });

  } catch (err) {
    area.innerHTML = `<div class="text-danger">❌ Mesajlar alınamadı.</div>`;
    console.error("Mesaj getirme hatası:", err);
  }
}

 
// Sohbet sayfası açıldığında çalışır
window.onload = function () {
  //const token = localStorage.getItem("token");
  const user = localStorage.getItem("chitchat_user");
   if (user) {
    const navUser = document.getElementById("navUserName");
    if (navUser) {
      navUser.innerText = `👋 ${user.name}`;
    }
  }

 // if (!token || !user) {
    // Token veya kullanıcı bilgisi yoksa giriş ekranına gönder
    //window.location.href = "index.html";
   // return;
  //}

  if (window.location.pathname.includes("chat.html")) {
    loadUser();
    document.getElementById("btnChat").click();
  }
   const searchInput = document.getElementById("userSearch");
  if (searchInput) {
    searchInput.addEventListener("input", (e) => {
      renderUserList(e.target.value);
    });
  }
};
function logout() {
  localStorage.removeItem("token");
  window.location.href = "index.html";
}
async function addFriend() {
  const email = document.getElementById("friendEmail").value.trim();
  if (!email) {
    alert("Lütfen bir e-posta girin.");
    return;
  }

  try {
    const res = await fetch(`${backendUrl}/api/users`);
    const users = await res.json();
    const match = users.find(u => u.email === email);

    if (!match) {
      alert("❌ Bu e-posta ile kayıtlı kullanıcı bulunamadı.");
      return;
    }

    let friends = JSON.parse(localStorage.getItem("chitchat_friends")) || [];
    if (friends.some(f => f.email === email)) {
      alert("Zaten arkadaş listenizde.");
      return;
    }

    friends.push({ name: match.name, gender: match.gender, email: match.email });
    localStorage.setItem("chitchat_friends", JSON.stringify(friends));
    renderUserList();
    alert("✅ Arkadaş eklendi.");
    document.getElementById("friendEmail").value = "";
  } catch (err) {
    console.error("Arkadaş ekleme hatası:", err);
    alert("❌ Bir hata oluştu.");
  }
}
function showNotification(message) {
  const notif = document.getElementById("notification");
  const notifText = document.getElementById("notificationText");
  notifText.innerText = message || "Yeni mesaj geldi!";
  notif.style.display = "block";

    // Sesli bildirim oynat
  const audio = document.getElementById("notifSound");
if (audio) {
  audio.currentTime = 0; // Baştan başlat
  audio.play().catch(err => {
    console.warn("Ses oynatılamadı:", err);
  });
}

  setTimeout(() => {
    notif.style.display = "none";
  }, 4000); // 4 saniye sonra gizle
}

function hideNotification() {
  document.getElementById("notification").style.display = "none";
}
function resetPassword() {
  const email = document.getElementById("resetEmail").value.trim();
  if (!email) {
    alert("Lütfen e-posta adresinizi girin.");
    return;
  }

  alert("📧 Şifre sıfırlama bağlantısı e-posta ile gönderilecek (simülasyon).");

  // Gerçek API'ye bağlamak istersen buraya POST isteği eklersin:
  // fetch('/api/reset-password', {
  //   method: 'POST',
  //   headers: { 'Content-Type': 'application/json' },
  //   body: JSON.stringify({ email })
  // });
}



